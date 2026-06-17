using Duende.IdentityServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server;
using Server.Authorization;
using Server.Services;
using SSO.Application.Abstractions;
using SSO.Domain.Identity;
using SSO.Infrastructure;
using SSO.Infrastructure.Persistence;
using SSO.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

var authority = builder.Configuration["Oidc:Authority"] ?? "https://localhost:5001";
var bffClientId = builder.Configuration["Oidc:ClientId"] ?? "fundadmin-bff";
// Secret comes from configuration (appsettings.Development.json / user-secrets / env).
var bffClientSecret = builder.Configuration["Oidc:ClientSecret"] ?? "fundadmin-bff-dev-secret";

// Dev-only handler that accepts the local self-signed cert on the BFF back channel.
static HttpClientHandler DevBackchannelHandler() => new()
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};

// =========================================================================
// 1. PERSISTENCE + IDENTITY
//    SsoDbContext (SQL Server) + the audit interceptor are registered by
//    AddSsoInfrastructure. ASP.NET Core Identity is layered on top here, in the
//    host, because SignInManager lives in the ASP.NET Core shared framework.
//    AddIdentityCore (NOT AddIdentity) is used on purpose: it does not register
//    cookie schemes, so the BFF "cookie"/"oidc" schemes below are left intact.
// =========================================================================
builder.Services.AddSsoInfrastructure(builder.Configuration);

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Password policy (NIST-aligned). Tune per the security baseline.
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Adaptive lockout / brute-force protection.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false; // dev; tighten in prod
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<SsoDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Protects per-user TOTP secrets at rest. In production escrow these keys to an
// HSM / Vault (architecture §6.3); the file system is a dev convenience.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "dp-keys")))
    .SetApplicationName("SSO-PROJECT");

// Audit context (who/where) resolved from the current request.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditContextProvider, HttpContextAuditContextProvider>();

// =========================================================================
// 2. IDENTITYSERVER (interactive IdP)
//    Users now come from ASP.NET Core Identity; claims (role + permission) are
//    issued by SsoProfileService. The "idsrv" cookie remains the IdP session.
// =========================================================================
var identityServerBuilder = builder.Services.AddIdentityServer(options =>
    {
        options.IssuerUri = authority;
        options.Authentication.CookieAuthenticationScheme =
            IdentityServerConstants.DefaultCookieAuthenticationScheme; // "idsrv"

        // Dev: disable automatic key management (which needs a license and a
        // Data-Protection-backed key store). We use a static developer signing key
        // instead, so token signing never depends on a DP key ring that may have
        // been reset ("key not found in the key ring"). Production keeps automatic
        // key management (licensed) or a configured certificate.
        if (builder.Environment.IsDevelopment())
            options.KeyManagement.Enabled = false;
    })
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryClients(Config.Clients(bffClientSecret))
    .AddProfileService<SsoProfileService>();

if (builder.Environment.IsDevelopment())
    identityServerBuilder.AddDeveloperSigningCredential(); // creates/loads tempkey.jwk

// =========================================================================
// 3. BFF AUTHENTICATION (Backend-for-Frontend) — unchanged, proven wiring.
// =========================================================================
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "cookie";
        options.DefaultChallengeScheme = "oidc";
        options.DefaultSignOutScheme = "oidc";
    })
    .AddCookie("cookie", options =>
    {
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

        options.ResponseType = "code";
        options.ResponseMode = "query";
        options.UsePkce = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("fundadmin.api");
        options.Scope.Add("offline_access");

        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;

        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";

        options.BackchannelHttpHandler = DevBackchannelHandler();
    });

builder.Services.AddBff().AddServerSideSessions();

// Permission-based authorization: [Authorize(Policy = "perm:<code>")].
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services.AddSingleton<TotpService>();
builder.Services.AddSingleton<LoginStateProtector>();
builder.Services.AddControllers();
builder.Services.AddRazorPages();

var app = builder.Build();

// =========================================================================
// 4. DATABASE MIGRATE + SEED (apply schema, seed permissions/roles/menus/admin)
// =========================================================================
await using (var scope = app.Services.CreateAsyncScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();
    try
    {
        await sp.GetRequiredService<SsoDbContext>().Database.MigrateAsync();

        var seeder = sp.GetRequiredService<DbSeeder>();
        var adminEmail = builder.Configuration["Admin:Email"] ?? "admin@fundadmin.local";
        var adminPassword = builder.Configuration["Admin:Password"] ?? "Admin#FundAdmin2026!";
        await seeder.SeedAsync(adminEmail, adminPassword);
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "Database migrate/seed failed. Verify the 'Sso' connection string points at a reachable SQL Server and that migrations exist.");
        throw;
    }
}

// =========================================================================
// 5. MIDDLEWARE PIPELINE
// =========================================================================

// Security headers (defense-in-depth). CSP tuned for Blazor WASM.
// In Development we widen connect-src so VS Browser Link / hot-reload websockets
// (ws/wss to random localhost ports) are not blocked; production stays strict.
var isDevelopment = app.Environment.IsDevelopment();
var connectSrc = isDevelopment
    ? "'self' ws: wss: http://localhost:* https://localhost:*"
    : "'self'";

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        $"connect-src {connectSrc}; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";
    await next();
});

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseIdentityServer(); // runs UseAuthentication
app.UseBff();
app.UseAuthorization();

app.MapBffManagementEndpoints();
app.MapRazorPages();
app.MapControllers().AsBffApiEndpoint();
app.MapFallbackToFile("index.html");

app.Run();
