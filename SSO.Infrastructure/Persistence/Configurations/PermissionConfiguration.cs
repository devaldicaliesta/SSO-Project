using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSO.Domain.Rbac;

namespace SSO.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permissions");
        b.HasKey(p => p.Id);

        b.Property(p => p.Code).HasMaxLength(100).IsRequired();
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.Description).HasMaxLength(500);
        b.Property(p => p.Category).HasMaxLength(100);

        // The permission code is the stable contract checked by authorization.
        b.HasIndex(p => p.Code).IsUnique();
    }
}
