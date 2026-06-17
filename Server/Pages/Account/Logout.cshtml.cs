using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SSO.Application.Abstractions;
using SSO.Domain.Auditing;
using SSO.Domain.Enums;

namespace Server.Pages.Account;

/// <summary>
/// Handles the OIDC end-session (RP-initiated logout). Clears the IdentityServer
/// session, records the logout in the audit trail, and bounces back to the
/// validated post-logout redirect URI.
/// </summary>
public class LogoutModel : PageModel
{
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IAuditService _audit;

    public LogoutModel(IIdentityServerInteractionService interaction, IAuditService audit)
    {
        _interaction = interaction;
        _audit = audit;
    }

    public async Task<IActionResult> OnGetAsync(string? logoutId)
    {
        var context = await _interaction.GetLogoutContextAsync(logoutId);

        if (User.Identity?.IsAuthenticated == true)
        {
            await _audit.RecordAsync(new AuditEvent
            {
                Category = AuditCategory.Authentication,
                Action = "Logout",
                Outcome = AuditOutcome.Success,
                Severity = AuditSeverity.Info
            });
        }

        // Sign out of the IdP's own cookie scheme explicitly.
        await HttpContext.SignOutAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme);

        if (!string.IsNullOrWhiteSpace(context?.PostLogoutRedirectUri))
            return Redirect(context.PostLogoutRedirectUri);

        return Redirect("~/");
    }
}
