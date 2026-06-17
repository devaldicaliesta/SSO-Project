using Duende.IdentityServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Services;
using SSO.Application.Abstractions;
using SSO.Domain.Auditing;
using SSO.Domain.Enums;
using SSO.Domain.Identity;

namespace Server.Pages.Account;

/// <summary>
/// Forced password change. Reached after password+TOTP succeed for a user flagged
/// <c>MustChangePassword</c>. The pending state is carried by the signed token
/// (same one issued at login). The session is only established once the password
/// has been rotated, subject to the full policy including no-reuse.
/// </summary>
public class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPasswordHistoryService _passwordHistory;
    private readonly IAuditService _audit;
    private readonly LoginStateProtector _loginState;

    public ChangePasswordModel(
        UserManager<ApplicationUser> users,
        IPasswordHistoryService passwordHistory,
        IAuditService audit,
        LoginStateProtector loginState)
    {
        _users = users;
        _passwordHistory = passwordHistory;
        _audit = audit;
        _loginState = loginState;
    }

    [BindProperty(SupportsGet = true)] public string? T { get; set; }
    [BindProperty] public string NewPassword { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;

    public string? Error { get; set; }
    public string Account { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!_loginState.TryUnprotect(T, out var sub, out _))
            return RedirectToPage("Login", new { expired = true });

        var user = await _users.FindByIdAsync(sub);
        Account = user?.UserName ?? "";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!_loginState.TryUnprotect(T, out var sub, out var returnUrl))
            return RedirectToPage("Login", new { expired = true });

        var user = await _users.FindByIdAsync(sub);
        if (user is null)
            return RedirectToPage("Login");

        Account = user.UserName ?? "";

        if (NewPassword != ConfirmPassword)
        {
            Error = "Konfirmasi password tidak cocok.";
            return Page();
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, NewPassword);
        if (!result.Succeeded)
        {
            Error = string.Join(" ", result.Errors.Select(e => e.Description));
            return Page();
        }

        user.MustChangePassword = false;
        user.PasswordChangedAtUtc = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user);
        await _passwordHistory.RecordCurrentAsync(user);

        await _audit.RecordAsync(new AuditEvent
        {
            Category = AuditCategory.UserManagement,
            Action = "Password.Changed",
            Outcome = AuditOutcome.Success,
            Severity = AuditSeverity.Info,
            ActorUserId = user.Id,
            ActorUserName = user.UserName,
            TargetType = nameof(ApplicationUser),
            TargetId = user.Id.ToString()
        });

        // Complete the deferred sign-in (password + TOTP already satisfied).
        var identityServerUser = new IdentityServerUser(user.Id.ToString())
        {
            DisplayName = user.UserName,
            AuthenticationMethods = new[] { "pwd", "mfa" }
        };
        await HttpContext.SignInAsync(
            IdentityServerConstants.DefaultCookieAuthenticationScheme,
            identityServerUser.CreatePrincipal());

        user.LastLoginAtUtc = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user);

        await _audit.RecordAsync(new AuditEvent
        {
            Category = AuditCategory.Authentication,
            Action = "Login.Success",
            Outcome = AuditOutcome.Success,
            Severity = AuditSeverity.Info,
            ActorUserId = user.Id,
            ActorUserName = user.UserName
        });

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : Redirect("~/");
    }
}
