using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Server.Pages.Account;

/// <summary>
/// Interactive login page for the in-memory dev Identity Provider.
/// IdentityServer redirects the browser here (its configured LoginUrl,
/// "/Account/Login") whenever an authorize request has no active session.
/// </summary>
public class LoginModel : PageModel
{
    private readonly TestUserStore _users;
    private readonly IIdentityServerInteractionService _interaction;

    public LoginModel(TestUserStore users, IIdentityServerInteractionService interaction)
    {
        _users = users;
        _interaction = interaction;
    }

    // Prefilled with the dev test user to make manual testing fast.
    [BindProperty] public string Username { get; set; } = "analyst@fundadmin.local";
    [BindProperty] public string Password { get; set; } = "Passw0rd!";

    // The IdentityServer /connect/authorize callback to resume after sign-in.
    [BindProperty] public string? ReturnUrl { get; set; }

    public string? Error { get; set; }

    public void OnGet(string? returnUrl)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_users.ValidateCredentials(Username, Password))
        {
            var user = _users.FindByUsername(Username);

            // Issue the authentication cookie (default "idsrv" sign-in scheme).
            // The user's claims flow into the tokens via the profile service.
            var identityServerUser = new IdentityServerUser(user.SubjectId)
            {
                DisplayName = user.Username,
                AdditionalClaims = user.Claims.ToArray()
            };
            // Sign into the IdP's own "idsrv" scheme explicitly. The application
            // default sign-in scheme is now the BFF "cookie", so we must NOT rely
            // on the default here, otherwise the IdP would not see a session.
            await HttpContext.SignInAsync(
                IdentityServerConstants.DefaultCookieAuthenticationScheme,
                identityServerUser.CreatePrincipal());

            // ReturnUrl points back into the authorize endpoint (a local URL).
            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }

            return Redirect("~/");
        }

        Error = "Invalid username or password.";
        return Page();
    }
}
