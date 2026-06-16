using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Server;

var builder = WebApplication.CreateBuilder(args);

// The Authority is read from configuration so switching to a production provider
// only requires editing appsettings.json (see TODO below).
var authority = builder.Configuration["Oidc:Authority"] ?? "https://localhost:5001";

// =========================================================================
// 1. DUENDE IDENTITYSERVER (IN-MEMORY DEV IDENTITY PROVIDER)
//
//    This makes the solution self-contained: a single `dotnet run` hosts the
//    Blazor client, the API and the OIDC provider on the same origin.
//
//    TODO: To switch to a production provider (Azure AD / Keycloak):
//          - Delete this AddIdentityServer(...) block and `app.UseIdentityServer()`.
//          - Remove the Account login/logout Razor Pages.
//          - Update Authority + ClientId + RedirectUris in:
//              * Client/wwwroot/appsettings.json  ("Oidc")
//              * Server/appsettings.json           ("Oidc")
//          - No other code changes are required.
// =========================================================================
builder.Services.AddIdentityServer(options =>
    {
        // Issuer must match the Authority configured on the Client and the API.
        options.IssuerUri = authority;
    })
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryClients(Config.Clients)
    .AddTestUsers(Config.TestUsers);
// NOTE: AddIdentityServer() registers the interactive "idsrv" cookie scheme and
//       sets it as the default authentication scheme. We must NOT override that
//       default, otherwise the browser-based login cannot establish a session.

// =========================================================================
// 2. JWT BEARER VALIDATION FOR THE API (named "Bearer" scheme)
//
//    Registered WITHOUT a default scheme argument so IdentityServer keeps its
//    cookie scheme as the application default. The API explicitly opts into the
//    "Bearer" scheme via [Authorize(AuthenticationSchemes = "Bearer")].
// =========================================================================
builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Authority = authority;

        // Keep the original JWT claim names ("name", "email", "role") instead of
        // remapping them to the legacy WS-* URIs, so the API can read them directly.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            // The dev IdP issues scope-based access tokens (no fixed audience),
            // so we validate the issuer/signature but not the audience.
            ValidateAudience = false,
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        // Dev only: the server validates tokens against its own HTTPS endpoint.
        // Accept the local self-signed dev certificate on the back channel.
        options.BackchannelHttpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddRazorPages(); // hosts the interactive Account/Login + Account/Logout pages

var app = builder.Build();

// =========================================================================
// 3. MIDDLEWARE PIPELINE
//    Order matters: framework files & static assets first, then routing,
//    then IdentityServer (which also runs UseAuthentication), then authZ.
// =========================================================================
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseIdentityServer();   // serves /connect/*, /.well-known/* and runs authentication
app.UseAuthorization();

app.MapRazorPages();       // /Account/Login, /Account/Logout
app.MapControllers();      // /api/me
app.MapFallbackToFile("index.html"); // Blazor WASM client (all other routes)

app.Run();
