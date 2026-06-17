using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SSO.Domain.Auditing;
using SSO.Domain.Identity;
using SSO.Domain.Rbac;

namespace SSO.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the SSO master tier. Extends
/// <see cref="IdentityDbContext{TUser,TRole,TKey}"/> (so all ASP.NET Core Identity
/// tables come for free) and adds the RBAC (permissions, menus, overrides) and
/// activity-monitoring (audit) aggregates. Entity shaping lives in
/// <c>IEntityTypeConfiguration</c> classes applied from this assembly.
/// </summary>
public class SsoDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public SsoDbContext(DbContextOptions<SsoDbContext> options) : base(options) { }

    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // configures the Identity schema first
        builder.ApplyConfigurationsFromAssembly(typeof(SsoDbContext).Assembly);
    }
}
