using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSO.Domain.Rbac;

namespace SSO.Infrastructure.Persistence.Configurations;

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> b)
    {
        b.ToTable("Menus");
        b.HasKey(m => m.Id);

        b.Property(m => m.Code).HasMaxLength(100).IsRequired();
        b.Property(m => m.Label).HasMaxLength(200).IsRequired();
        b.Property(m => m.Icon).HasMaxLength(100);
        b.Property(m => m.Route).HasMaxLength(300);

        b.HasIndex(m => m.Code).IsUnique();

        // Self-referencing tree. Restrict delete so removing a parent with
        // children is an explicit, deliberate operation (no silent cascade).
        b.HasOne(m => m.Parent)
            .WithMany(m => m.Children)
            .HasForeignKey(m => m.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // A menu's visibility gate. Restrict delete so a permission still
        // referenced by a menu cannot be removed out from under it.
        b.HasOne(m => m.RequiredPermission)
            .WithMany(p => p.Menus)
            .HasForeignKey(m => m.RequiredPermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
