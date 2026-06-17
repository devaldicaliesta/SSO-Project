using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSO.Domain.Rbac;

namespace SSO.Infrastructure.Persistence.Configurations;

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> b)
    {
        b.ToTable("UserPermissions");

        // One override row per (user, permission): a user cannot simultaneously
        // grant and deny the same permission. Deny wins in the resolver.
        b.HasKey(up => new { up.UserId, up.PermissionId });

        b.Property(up => up.Type).HasConversion<string>().HasMaxLength(10);
        b.Property(up => up.Reason).HasMaxLength(500);

        b.HasOne(up => up.User)
            .WithMany(u => u.PermissionOverrides)
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(up => up.Permission)
            .WithMany(p => p.UserPermissions)
            .HasForeignKey(up => up.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
