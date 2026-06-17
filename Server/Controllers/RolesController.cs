using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared;
using SSO.Application.Abstractions;
using SSO.Domain.Auditing;
using SSO.Domain.Enums;
using SSO.Domain.Identity;
using SSO.Domain.Rbac;
using SSO.Infrastructure.Persistence;

namespace Server.Controllers;

/// <summary>
/// Role + role-permission administration. Requires <c>admin.roles.manage</c>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "perm:admin.roles.manage")]
public class RolesController : ControllerBase
{
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly SsoDbContext _db;
    private readonly IAuditService _audit;

    public RolesController(RoleManager<ApplicationRole> roles, SsoDbContext db, IAuditService audit)
    {
        _roles = roles;
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> List(CancellationToken ct)
    {
        var roles = await _roles.Roles.OrderBy(r => r.Name).ToListAsync(ct);
        var rolePermissions = await _db.RolePermissions
            .Select(rp => new { rp.RoleId, rp.Permission!.Code })
            .ToListAsync(ct);

        var dtos = roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name ?? string.Empty,
            Description = r.Description,
            IsSystemRole = r.IsSystemRole,
            Permissions = rolePermissions.Where(rp => rp.RoleId == r.Id).Select(rp => rp.Code).ToList()
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost]
    public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Role name is required.");

        if (await _roles.RoleExistsAsync(req.Name))
            return Conflict("A role with that name already exists.");

        var role = new ApplicationRole(req.Name) { Description = req.Description };
        var created = await _roles.CreateAsync(role);
        if (!created.Succeeded)
            return BadRequest(string.Join("; ", created.Errors.Select(e => e.Description)));

        await ReplacePermissionsAsync(role.Id, req.Permissions, ct);
        await AuditAsync("Role.Created", role.Id, $"{{\"name\":\"{role.Name}\"}}");

        return Ok(new RoleDto { Id = role.Id, Name = role.Name!, Description = role.Description, Permissions = req.Permissions });
    }

    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> SetPermissions(Guid id, [FromBody] SetRolePermissionsRequest req, CancellationToken ct)
    {
        var role = await _roles.FindByIdAsync(id.ToString());
        if (role is null) return NotFound();

        await ReplacePermissionsAsync(id, req.Permissions, ct);
        await AuditAsync("Role.PermissionsChanged", id, $"{{\"permissions\":\"{string.Join(',', req.Permissions)}\"}}");

        return NoContent();
    }

    private async Task ReplacePermissionsAsync(Guid roleId, IEnumerable<string> permissionCodes, CancellationToken ct)
    {
        var existing = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        _db.RolePermissions.RemoveRange(existing);

        var ids = await _db.Permissions
            .Where(p => permissionCodes.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var permissionId in ids)
            _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });

        await _db.SaveChangesAsync(ct);
    }

    private Task AuditAsync(string action, Guid roleId, string? details) =>
        _audit.RecordAsync(new AuditEvent
        {
            Category = AuditCategory.UserManagement,
            Action = action,
            Outcome = AuditOutcome.Success,
            Severity = AuditSeverity.Warning,
            TargetType = nameof(ApplicationRole),
            TargetId = roleId.ToString(),
            DetailsJson = details
        });
}
