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
                ClientId = "fundadmin-bff",
                ClientName = "FundAdmin Asset Management (BFF)",

                // CONFIDENTIAL client: the OIDC code exchange happens server-side
                // (the BFF host), so the secret CAN be stored safely here. Tokens
                // never reach the browser - the browser only gets an HttpOnly cookie.
                // TODO: in production load the secret from configuration / a secret
                //       store, do NOT hard-code it.
                AllowedGrantTypes = GrantTypes.Code,
                RequireClientSecret = true,
                ClientSecrets = { new Secret("fundadmin-bff-dev-secret".Sha256()) },
                RequirePkce = true,

                // No consent screen for this first-party application.
                RequireConsent = false,

                // The BFF (ASP.NET Core OpenIdConnect handler) callback endpoints.
                RedirectUris = { "https://localhost:5001/signin-oidc" },
                PostLogoutRedirectUris = { "https://localhost:5001/signout-callback-oidc" },

                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    "fundadmin.api"
                },

                // Refresh tokens (held server-side by the BFF) so the session can be
                // silently renewed without bouncing the user through the IdP again.
                AllowOfflineAccess = true,

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

    // Dev TOTP secrets (Base32) per user subject id, used for the second factor.
    // TODO: in production generate a unique secret per user at enrollment time and
    //       store it securely (encrypted) - never hard-code shared dev secrets.
    private static readonly IReadOnlyDictionary<string, string> MfaSecrets =
        new Dictionary<string, string>
        {
            ["1"] = "FUNDADMINDEVSECRET234567"
        };

    public static string GetMfaSecret(string subjectId) =>
        MfaSecrets.TryGetValue(subjectId, out var secret) ? secret : "FUNDADMINDEVSECRET234567";
}
