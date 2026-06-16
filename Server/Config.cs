using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using System.Security.Claims;

// The Server references the "Client" Blazor project, whose root namespace
// "Client" collides with Duende's Client type. Alias it to disambiguate.
using IsClient = Duende.IdentityServer.Models.Client;

namespace Server;

/// <summary>
/// In-memory configuration for the development Identity Provider.
/// All values are kept consistent with the Client/Server appsettings.json.
/// </summary>
public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources
    {
        get
        {
            // Expose the "role" claim through the standard "profile" scope so it is
            // included in the ID token (the Client reads name/role from the token,
            // and the Client requests AlwaysIncludeUserClaimsInIdToken below).
            var profile = new IdentityResources.Profile();
            profile.UserClaims.Add("role");

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
            // requested, allowing the protected /api/me endpoint to read them.
            new ApiScope("fundadmin.api", "FundAdmin Asset Management API")
            {
                UserClaims = { "name", "email", "role" }
            }
        };

    public static IEnumerable<IsClient> Clients =>
        new List<IsClient>
        {
            new IsClient
            {
                ClientId = "fundadmin-wasm",
                ClientName = "FundAdmin Asset Management (Blazor WASM)",

                // Public client: PKCE, no client secret (the secret can never be
                // stored safely in a browser-hosted SPA).
                AllowedGrantTypes = GrantTypes.Code,
                RequireClientSecret = false,
                RequirePkce = true,

                // No consent screen for this first-party application.
                RequireConsent = false,

                RedirectUris = { "https://localhost:5001/authentication/login-callback" },
                PostLogoutRedirectUris = { "https://localhost:5001/authentication/logout-callback" },
                AllowedCorsOrigins = { "https://localhost:5001" },

                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    "fundadmin.api"
                },

                // The WASM auth library does not call the userinfo endpoint by
                // default, so include the user claims directly in the ID token.
                AlwaysIncludeUserClaimsInIdToken = true,
                AccessTokenLifetime = 3600
            }
        };

    public static List<TestUser> TestUsers =>
        new List<TestUser>
        {
            new TestUser
            {
                SubjectId = "1",
                Username = "analyst@fundadmin.local",
                Password = "Passw0rd!",
                Claims =
                {
                    new Claim("name", "Andi Analyst"),
                    new Claim("email", "analyst@fundadmin.local"),
                    new Claim("role", "InvestmentManager")
                }
            }
        };
}
