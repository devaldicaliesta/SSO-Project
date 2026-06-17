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
/// First factor (password) for the IdP, backed by ASP.NET Core Identity. On
/// success the session is NOT established yet — the pending subject is carried to
/// the TOTP step. Failed/locked/disabled attempts are written to the audit trail.
/// </summary>
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditService _audit;
    private readonly LoginStateProtector _loginState;

    public LoginModel(
        SignInManager<ApplicationUser> signIn,
        UserManager<ApplicationUser> users,
        IAuditService audit,
        LoginStateProtector loginState)
    {
        _signIn = signIn;
        _users = users;
        _audit = audit;
        _loginState = loginState;
    }

    [BindProperty] public string Username { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;

    // The IdentityServer /connect/authorize callback to resume after sign-in.
    [BindProperty] public string? ReturnUrl { get; set; }

    // Set when the MFA/change-password token expired and the user was sent back.
    [BindProperty(SupportsGet = true)] public bool Expired { get; set; }

    public string? Error { get; set; }

    public void OnGet(string? returnUrl)
    {
        ReturnUrl = returnUrl;
        if (Expired)
            Error = "Sesi login kedaluwarsa. Silakan masuk kembali.";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _users.FindByNameAsync(Username) ?? await _users.FindByEmailAsync(Username);

        if (user is null)
        {
            await AuditAsync("Login.Password.Failed", AuditOutcome.Failure, AuditSeverity.Warning,
                actorName: Username, reason: "unknown-user");
            Error = "Invalid username or password.";
            return Page();
        }

        if (user.Status != UserStatus.Active)
        {
            await AuditAsync("Login.Denied.Disabled", AuditOutcome.Failure, AuditSeverity.Warning,
                actorId: user.Id, actorName: user.UserName);
            Error = "This account is not active. Please contact an administrator.";
            return Page();
        }

        var result = await _signIn.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            await AuditAsync("Login.LockedOut", AuditOutcome.Failure, AuditSeverity.Critical,
                actorId: user.Id, actorName: user.UserName);
            Error = "Account temporarily locked due to repeated failed attempts. Try again later.";
            return Page();
        }

        if (!result.Succeeded)
        {
            await AuditAsync("Login.Password.Failed", AuditOutcome.Failure, AuditSeverity.Warning,
                actorId: user.Id, actorName: user.UserName, reason: "bad-password");
            Error = "Invalid username or password.";
            return Page();
        }

        // First factor verified. Defer the session until TOTP is confirmed; carry
        // the pending state in a signed, time-limited token (not a TempData cookie).
        await AuditAsync("Login.Password.Success", AuditOutcome.Success, AuditSeverity.Info,
            actorId: user.Id, actorName: user.UserName);

        var returnUrl = (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)) ? ReturnUrl : "~/";
        var token = _loginState.Protect(user.Id.ToString(), returnUrl);

        return RedirectToPage("Mfa", new { t = token });
    }

    private Task AuditAsync(string action, AuditOutcome outcome, AuditSeverity severity,
        Guid? actorId = null, string? actorName = null, string? reason = null) =>
        _audit.RecordAsync(new AuditEvent
        {
            Category = AuditCategory.Authentication,
            Action = action,
            Outcome = outcome,
            Severity = severity,
            ActorUserId = actorId,
            ActorUserName = actorName,
            DetailsJson = reason is null ? null : $"{{\"reason\":\"{reason}\"}}"
        });
}
