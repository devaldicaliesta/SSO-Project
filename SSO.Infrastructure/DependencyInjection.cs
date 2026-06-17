using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SSO.Application.Abstractions;
using SSO.Domain.Identity;
using SSO.Infrastructure.Auditing;
using SSO.Infrastructure.Persistence;
using SSO.Infrastructure.Rbac;
using SSO.Infrastructure.Security;
using SSO.Infrastructure.Seeding;

namespace SSO.Infrastructure;

/// <summary>
/// Registers the persistence + domain-service side of the SSO master tier. Identity
/// (UserManager/RoleManager/SignInManager) and Data Protection are configured by the
/// host (which references the ASP.NET Core shared framework); this method wires the
/// DbContext (with the audit interceptor) and the RBAC/audit/MFA services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSsoInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Sso")
            ?? throw new InvalidOperationException(
                "Connection string 'Sso' is not configured (ConnectionStrings:Sso).");

        // The audit interceptor needs the per-request IAuditContextProvider, so it is
        // scoped and resolved through the service-provider-aware options overload.
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<SsoDbContext>((sp, options) =>
            options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(typeof(SsoDbContext).Assembly.FullName))
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<IMfaSecretProtector, MfaSecretProtector>();
        services.AddScoped<IPasswordHistoryService, PasswordHistoryService>();

        // Extra Identity password validator: no reuse of recent passwords.
        services.AddScoped<IPasswordValidator<ApplicationUser>, PasswordHistoryValidator>();

        services.AddScoped<DbSeeder>();

        return services;
    }
}
