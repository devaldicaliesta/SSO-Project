using Duende.IdentityServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Server;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

// The Authority is read from configuration so switching to a production provider
// only requires editing appsettings.json (see TODO below).
var authority = builder.Configuration["Oidc:Authority"] ?? "https://localhost:5001";
var bffClientId = builder.Configuration["Oidc:ClientId"] ?? "fundadmin-bff";
// TODO: in production load the secret from configuration / a secret store.
var bffClientSecret = builder.Configuration["Oidc:ClientSecret"] ?? "fundadmin-bff-dev-secret";

// A dev-only handler that accepts the local self-signed cert on the back channel
// (the BFF talks to the IdP over https://localhost on the same machine).
static HttpClientHandler DevBackchannelHandler() => new()
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};

// =========================================================================
// 1. DUENDE IDENTITYSERVER (IN-MEMORY DEV IDENTITY PROVIDER)
//
//    Same as before: issues tokens and hosts the interactive login UI under
//    its own "idsrv" cookie scheme.
//
//    TODO: To switch to a production provider (Azure AD / Keycloak):
//          - Delete this AddIdentityServer(...) block and `app.UseIdentityServer()`.
//          - Remove the Account login/logout Razor Pages.
//          - Update Authority + ClientId + ClientSecret + redirect URIs in
//            Server/appsettings.json ("Oidc") and register the same redirect URIs
//            (/signin-oidc, /signout-callback-oidc) in the real provider.
// =========================================================================
builder.Services.AddIdentityServer(options =>
    {
        options.IssuerUri = authority;

        // The application default scheme is the BFF "cookie". Without this line
        // IdentityServer would read its login session from that default scheme
        // and never see the idsrv session created by the Account/Login page,
        // causing an infinite "User is not authenticated" login loop. Pin it.
        options.Authentication.CookieAuthenticationScheme =
            IdentityServerConstants.DefaultCookieAuthenticationScheme; // "idsrv"
    })
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryClients(Config.Clients)
    .AddTestUsers(Config.TestUsers);

// =========================================================================
// 2. BFF AUTHENTICATION (Backend-for-Frontend)
//
//    The SERVER is now a CONFIDENTIAL OIDC client. The Authorization Code +
//    PKCE exchange happens here, server-side. Tokens are kept on the server;
//    the browser only ever receives an encrypted, HttpOnly session cookie.
//
//    - "cookie" : the BFF session cookie (default scheme for the app).
//    - "oidc"   : challenges the IdP to sign the user in.
// =========================================================================
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "cookie";
        options.DefaultChallengeScheme = "oidc";
        options.DefaultSignOutScheme = "oidc";
    })
    .AddCookie("cookie", options =>
    {
        // HttpOnly => unreadable by JavaScript (immune to XSS token theft).
        // Secure + SameSite=Strict => not sent on cross-site requests.
        options.Cookie.Name = "__Host-fundadmin-bff";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority = authority;
        options.ClientId = bffClientId;
        options.ClientSecret = bffClientSecret;

        // Authorization Code Flow + PKCE, done entirely on the server.
        options.ResponseType = "code";
        options.ResponseMode = "query";
        options.UsePkce = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("fundadmin.api");
        options.Scope.Add("offline_access"); // refresh token for silent renewal

        // Keep the tokens server-side and pull the user claims from userinfo.
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;

        // Keep original JWT claim names ("name", "role") instead of WS-* URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";

        // Dev only: accept the local self-signed cert on the back channel.
        options.BackchannelHttpHandler = DevBackchannelHandler();
    });

// Duende BFF: session management + secure /bff/* endpoints (login, logout, user)
// and antiforgery protection for local API calls.
builder.Services.AddBff()
    .AddServerSideSessions(); // in-memory session store (refresh-token revocation, etc.)

builder.Services.AddSingleton<TotpService>(); // TOTP second factor for the dev IdP
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddRazorPages(); // hosts the interactive Account/Login + Account/Mfa + Account/Logout pages

var app = builder.Build();

// =========================================================================
// 3. MIDDLEWARE PIPELINE
//    Order matters: framework files & static assets, routing, IdentityServer
//    (runs UseAuthentication), then BFF, then authorization.
// =========================================================================
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseIdentityServer(); // serves /connect/*, /.well-known/* and runs authentication
app.UseBff();            // BFF session + antiforgery middleware
app.UseAuthorization();

// BFF management endpoints: /bff/login, /bff/logout, /bff/user.
app.MapBffManagementEndpoints();

app.MapRazorPages();                       // /Account/Login, /Account/Logout (IdP UI)
app.MapControllers().AsBffApiEndpoint();   // /api/* protected by cookie + antiforgery
app.MapFallbackToFile("index.html");       // Blazor WASM client (all other routes)

app.Run();
