using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Test;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Services;

namespace Server.Pages.Account;

/// <summary>
/// Second factor (TOTP) for the dev IdP. Reached only after the password step
/// succeeds (the pending subject id is carried in TempData). On a valid code the
/// idsrv session is finally established with amr = [pwd, mfa].
/// </summary>
public class MfaModel : PageModel
{
    private readonly TestUserStore _users;
    private readonly TotpService _totp;

    public MfaModel(TestUserStore users, TotpService totp)
    {
        _users = users;
        _totp = totp;
    }

    // Cookie that remembers this browser has already added the account to its
    // authenticator app, so we don't push the enrollment QR on every login
    // (which makes Microsoft Authenticator complain the account already exists).
    private const string EnrolledCookie = "fa_enrolled";

    [BindProperty] public string? Code { get; set; }

    public string? Error { get; set; }
    public string Account { get; set; } = "";
    public string QrDataUri { get; set; } = "";
    public string ManualKey { get; set; } = "";

    // When true the QR + manual key are collapsed (returning user just types a code).
    public bool AlreadyEnrolled { get; set; }

    public IActionResult OnGet()
    {
        var sub = TempData.Peek("mfa:sub") as string;
        if (string.IsNullOrEmpty(sub))
        {
            // No pending password step -> start over.
            return RedirectToPage("Login");
        }

        Prepare(sub);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var sub = TempData.Peek("mfa:sub") as string;
        if (string.IsNullOrEmpty(sub))
        {
            return RedirectToPage("Login");
        }

        var user = _users.FindBySubjectId(sub);
        var secret = Config.GetMfaSecret(sub);

        if (user is null || !_totp.VerifyCode(secret, Code))
        {
            Error = "Kode autentikator tidak valid. Coba lagi.";
            Prepare(sub);
            return Page();
        }

        var returnUrl = TempData.Peek("mfa:returnUrl") as string ?? "~/";

        // Remember (on this browser) that the authenticator is set up, so future
        // logins skip the enrollment QR and only ask for the 6-digit code.
        Response.Cookies.Append(EnrolledCookie, "1", new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });

        // Both factors satisfied -> establish the idsrv session now.
        var identityServerUser = new IdentityServerUser(user.SubjectId)
        {
            DisplayName = user.Username,
            AuthenticationMethods = new[] { "pwd", "mfa" }, // amr claims
            AdditionalClaims = user.Claims.ToArray()
        };
        await HttpContext.SignInAsync(
            IdentityServerConstants.DefaultCookieAuthenticationScheme,
            identityServerUser.CreatePrincipal());

        // Pending state consumed; clear it.
        TempData.Remove("mfa:sub");
        TempData.Remove("mfa:returnUrl");

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : Redirect("~/");
    }

    // Builds the enrollment data (QR + manual key) shown on the page.
    private void Prepare(string sub)
    {
        var user = _users.FindBySubjectId(sub);
        Account = user?.Username ?? "user";

        AlreadyEnrolled = Request.Cookies[EnrolledCookie] == "1";

        var secret = Config.GetMfaSecret(sub);
        ManualKey = TotpService.FormatManualKey(secret);

        var uri = _totp.BuildOtpAuthUri(secret, Account);
        QrDataUri = _totp.GenerateQrPngDataUri(uri);

        // Keep the pending state alive for the next request (GET render -> POST).
        TempData.Keep("mfa:sub");
        TempData.Keep("mfa:returnUrl");
    }
}
