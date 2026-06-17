using Duende.IdentityServer;
using Duende.IdentityServer.Models;

// The Server references the "Client" Blazor project, whose root namespace
// "Client" collides with Duende's Client type. Alias it to disambiguate.
using IsClient = Duende.IdentityServer.Models.Client;

namespace Server;

/// <summary>
/// In-memory protocol configuration for the IdentityServer (resources, scopes,
/// clients). Users/credentials are NO LONGER configured here — they live in SQL
/// Server via ASP.NET Core Identity, and claims are issued by
/// <see cref="Server.Services.SsoProfileService"/>.
/// </summary>
public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources
    {
        get
        {
            // Expose "role" and "permission" through the standard "profile" scope so
            // they are issued to the ID token / userinfo (the BFF reads them back).
            var profile = new IdentityResources.Profile();
            profile.UserClaims.Add("role");
            profile.UserClaims.Add("permission");

            return new IdentityResource[]
            {
                new IdentityResources.OpenId(),
                profile,
                new IdentityResources.Email()
            };
        }
    }

    public static IEnumerable<ApiScope> ApiScopes =>
        new List<ApiScope>
        {
            // UserClaims here are added to the ACCESS token when the scope is
            // requested, so resource APIs can authorize on role/permission.
            new ApiScope("fundadmin.api", "FundAdmin Asset Management API")
            {
                UserClaims = { "name", "email", "role", "permission" }
            }
        };

    /// <summary>
    /// The first-party BFF client. The secret is supplied from configuration
    /// (never hard-coded) and stored hashed.
    /// </summary>
    public static IEnumerable<IsClient> Clients(string bffClientSecret) =>
        new List<IsClient>
        {
            new IsClient
            {
                ClientId = "fundadmin-bff",
                ClientName = "FundAdmin Asset Management (BFF)",

                // CONFIDENTIAL client: the OIDC code exchange happens server-side
                // (the BFF host). Tokens never reach the browser - it only gets an
                // HttpOnly cookie.
                AllowedGrantTypes = GrantTypes.Code,
                RequireClientSecret = true,
                ClientSecrets = { new Secret(bffClientSecret.Sha256()) },
                RequirePkce = true,

                RequireConsent = false,

                RedirectUris = { "https://localhost:5001/signin-oidc" },
                PostLogoutRedirectUris = { "https://localhost:5001/signout-callback-oidc" },

                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    "fundadmin.api"
                },

                AllowOfflineAccess = true,
                AlwaysIncludeUserClaimsInIdToken = true,
                AccessTokenLifetime = 3600
            }
        };
}
