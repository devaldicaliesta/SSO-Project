using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
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

    public IActionResult OnPost()
    {
        if (_users.ValidateCredentials(Username, Password))
        {
            var user = _users.FindByUsername(Username);

            // FIRST factor verified. Do NOT sign in yet - defer the session until
            // the TOTP second factor is confirmed on /Account/Mfa. The pending
            // state is carried in TempData (an encrypted, tamper-proof cookie).
            TempData["mfa:sub"] = user.SubjectId;
            TempData["mfa:returnUrl"] =
                (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)) ? ReturnUrl : "~/";

            return RedirectToPage("Mfa");
        }

        Error = "Invalid username or password.";
        return Page();
    }
}
