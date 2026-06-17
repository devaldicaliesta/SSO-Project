using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Services;
using Shared;
using SSO.Application.Abstractions;
using SSO.Domain.Auditing;
using SSO.Domain.Enums;
using SSO.Domain.Identity;

namespace Server.Controllers;

/// <summary>
/// Admin user lifecycle (Joiner-Mover-Leaver): list, create, enable/disable,
/// unlock, reset password, assign roles. Every action is written to the audit
/// trail. Requires the <c>admin.users.manage</c> permission.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "perm:admin.users.manage")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly IPasswordHistoryService _passwordHistory;
    private readonly IAuditService _audit;

    public UsersController(
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        IPasswordHistoryService passwordHistory,
        IAuditService audit)
    {
        _users = users;
        _roles = roles;
        _passwordHistory = passwordHistory;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> List(CancellationToken ct)
    {
        var users = await _users.Users.OrderBy(u => u.UserName).ToListAsync(ct);

        var result = new List<UserListItemDto>(users.Count);
        foreach (var u in users)
        {
            result.Add(new UserListItemDto
            {
                Id = u.Id,
                UserName = u.UserName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                FullName = u.FullName,
                Status = u.Status.ToString(),
                LockedOut = u.LockoutEnd is not null && u.LockoutEnd > DateTimeOffset.UtcNow,
                MfaEnrolled = u.MfaType != MfaType.None,
                MustChangePassword = u.MustChangePassword,
                LastLoginAtUtc = u.LastLoginAtUtc,
                Roles = (await _users.GetRolesAsync(u)).ToList()
            });
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TemporaryPasswordDto>> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest("Email is required.");

        if (await _users.FindByEmailAsync(req.Email) is not null)
            return Conflict("A user with that email already exists.");

        var tempPassword = string.IsNullOrWhiteSpace(req.Password) ? PasswordGenerator.Generate() : req.Password!;

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            FullName = req.FullName,
            Status = UserStatus.Active,
            EmailConfirmed = true,
            MustChangePassword = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PasswordChangedAtUtc = DateTimeOffset.UtcNow
        };

        var created = await _users.CreateAsync(user, tempPassword);
        if (!created.Succeeded)
            return BadRequest(Errors(created));

        foreach (var role in req.Roles)
            if (await _roles.RoleExistsAsync(role))
                await _users.AddToRoleAsync(user, role);

        await _passwordHistory.RecordCurrentAsync(user, ct);

        await AuditAsync("User.Created", user, AuditSeverity.Info);

        return Ok(new TemporaryPasswordDto { UserId = user.Id, TemporaryPassword = tempPassword });
    }

    [HttpPost("{id:guid}/disable")]
    public Task<IActionResult> Disable(Guid id) => SetStatus(id, UserStatus.Disabled, "User.Disabled", killSessions: true);

    [HttpPost("{id:guid}/enable")]
    public Task<IActionResult> Enable(Guid id) => SetStatus(id, UserStatus.Active, "User.Enabled", killSessions: false);

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        await AuditAsync("User.Unlocked", user, AuditSeverity.Info);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<TemporaryPasswordDto>> ResetPassword(Guid id, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var tempPassword = PasswordGenerator.Generate();
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var reset = await _users.ResetPasswordAsync(user, token, tempPassword);
        if (!reset.Succeeded)
            return BadRequest(Errors(reset));

        user.MustChangePassword = true;
        user.PasswordChangedAtUtc = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user);
        await _passwordHistory.RecordCurrentAsync(user, ct);

        await AuditAsync("User.PasswordReset", user, AuditSeverity.Warning);

        return Ok(new TemporaryPasswordDto { UserId = user.Id, TemporaryPassword = tempPassword });
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] SetUserRolesRequest req)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var current = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, current);

        var requested = new List<string>();
        foreach (var role in req.Roles)
            if (await _roles.RoleExistsAsync(role))
                requested.Add(role);

        if (requested.Count > 0)
            await _users.AddToRolesAsync(user, requested);

        // Invalidate existing tokens so the new role set takes effect.
        await _users.UpdateSecurityStampAsync(user);

        await AuditAsync("User.RolesChanged", user, AuditSeverity.Warning,
            details: $"{{\"roles\":\"{string.Join(',', requested)}\"}}");

        return NoContent();
    }

    private async Task<IActionResult> SetStatus(Guid id, UserStatus status, string action, bool killSessions)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        user.Status = status;
        await _users.UpdateAsync(user);
        if (killSessions)
            await _users.UpdateSecurityStampAsync(user);

        await AuditAsync(action, user, AuditSeverity.Warning);
        return NoContent();
    }

    private Task AuditAsync(string action, ApplicationUser target, AuditSeverity severity, string? details = null) =>
        _audit.RecordAsync(new AuditEvent
        {
            Category = AuditCategory.UserManagement,
            Action = action,
            Outcome = AuditOutcome.Success,
            Severity = severity,
            TargetType = nameof(ApplicationUser),
            TargetId = target.Id.ToString(),
            DetailsJson = details
        });

    private static string Errors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => e.Description));
}
