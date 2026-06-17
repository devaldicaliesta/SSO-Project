using Duende.IdentityServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SSO.Application.Abstractions;
using SSO.Domain.Auditing;
using SSO.Domain.Enums;
using SSO.Domain.Identity;
using Server.Services;

namespace Server.Pages.Account;

/// <summary>
/// Second factor (TOTP). Reached only after the password step, identified by the
/// signed, time-limited token in the query string (carried into the form). On a
/// valid code the IdentityServer session is established with amr = [pwd, mfa]
/// (unless a password change is mandated first).
/// </summary>
public class MfaModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly TotpService _totp;
    private readonly IMfaSecretProtector _protector;
    private readonly IAuditService _audit;
    private readonly LoginStateProtector _loginState;

    public MfaModel(
        UserManager<ApplicationUser> users,
        TotpService totp,
        IMfaSecretProtector protector,
        IAuditService audit,
        LoginStateProtector loginState)
    {
        _users = users;
        _totp = totp;
        _protector = protector;
        _audit = audit;
        _loginState = loginState;
    }

    private const string EnrolledCookie = "fa_enrolled";

    // The pending-login token (query string on GET, hidden field on POST).
    [BindProperty(SupportsGet = true)] public string? T { get; set; }
    [BindProperty] public string? Code { get; set; }

    public string? Error { get; set; }
    public string Account { get; set; } = "";
    public string QrDataUri { get; set; } = "";
    public string ManualKey { get; set; } = "";
    public bool AlreadyEnrolled { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!_loginState.TryUnprotect(T, out var sub, out _))
            return RedirectToPage("Login", new { expired = true });

        await PrepareAsync(sub);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!_loginState.TryUnprotect(T, out var sub, out var returnUrl))
            return RedirectToPage("Login", new { expired = true });

        var user = await _users.FindByIdAsync(sub);
        if (user is null)
            return RedirectToPage("Login");

        var secret = await EnsureSecretAsync(user);

        if (!_totp.VerifyCode(secret, Code))
        {
            await _audit.RecordAsync(new AuditEvent
            {
                Category = AuditCategory.Authentication,
                Action = "Login.Mfa.Failed",
                Outcome = AuditOutcome.Failure,
                Severity = AuditSeverity.Warning,
                ActorUserId = user.Id,
                ActorUserName = user.UserName
            });

            Error = "Kode autentikator tidak valid. Coba lagi.";
            await PrepareAsync(sub);
            return Page();
        }

        Response.Cookies.Append(EnrolledCookie, "1", new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });

        // Password + TOTP satisfied. If a password change is mandated, divert there
        // (carry the same token); the change-password page completes sign-in.
        if (user.MustChangePassword)
        {
            await _audit.RecordAsync(new AuditEvent
            {
                Category = AuditCategory.Authentication,
                Action = "Login.Mfa.Success",
                Outcome = AuditOutcome.Success,
                Severity = AuditSeverity.Info,
                ActorUserId = user.Id,
                ActorUserName = user.UserName
            });

            return RedirectToPage("ChangePassword", new { t = T });
        }

        await SignInAndAuditAsync(user);

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : Redirect("~/");
    }

    private async Task SignInAndAuditAsync(ApplicationUser user)
    {
        var identityServerUser = new IdentityServerUser(user.Id.ToString())
        {
            DisplayName = user.UserName,
            AuthenticationMethods = new[] { "pwd", "mfa" } // amr claims
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
    }

    /// <summary>Returns the user's plaintext TOTP secret, creating+storing one on first use.</summary>
    private async Task<string> EnsureSecretAsync(ApplicationUser user)
    {
        if (!string.IsNullOrEmpty(user.MfaSecretEncrypted))
        {
            try { return _protector.Unprotect(user.MfaSecretEncrypted); }
            catch { /* unreadable (key rotation/corruption) -> re-enroll below */ }
        }

        var secret = TotpService.GenerateSecret();
        user.MfaSecretEncrypted = _protector.Protect(secret);
        user.MfaType = MfaType.Totp;
        await _users.UpdateAsync(user);
        return secret;
    }

    private async Task PrepareAsync(string sub)
    {
        var user = await _users.FindByIdAsync(sub);
        Account = user?.UserName ?? "user";
        AlreadyEnrolled = Request.Cookies[EnrolledCookie] == "1";

        if (user is not null)
        {
            var secret = await EnsureSecretAsync(user);
            ManualKey = TotpService.FormatManualKey(secret);
            QrDataUri = _totp.GenerateQrPngDataUri(_totp.BuildOtpAuthUri(secret, Account));
        }
    }
}
