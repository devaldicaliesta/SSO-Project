using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Server.Pages.Account;

/// <summary>
/// Handles the OIDC end-session (RP-initiated logout) request. IdentityServer
/// redirects the browser here ("/Account/Logout") from its end-session endpoint.
/// We clear the local cookie session and bounce back to the client's
/// post-logout redirect URI (which already carries the round-tripped state).
/// </summary>
public class LogoutModel : PageModel
{
    private readonly IIdentityServerInteractionService _interaction;

    public LogoutModel(IIdentityServerInteractionService interaction)
    {
        _interaction = interaction;
    }

    public async Task<IActionResult> OnGetAsync(string? logoutId)
    {
        // Validates the request and yields the validated post-logout redirect URI.
        var context = await _interaction.GetLogoutContextAsync(logoutId);

        // Sign out of the IdP's own "idsrv" scheme explicitly (the application
        // default scheme is the BFF "cookie", not idsrv).
        await HttpContext.SignOutAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme);

        if (!string.IsNullOrWhiteSpace(context?.PostLogoutRedirectUri))
        {
            return Redirect(context.PostLogoutRedirectUri);
        }

        return Redirect("~/");
    }
}
