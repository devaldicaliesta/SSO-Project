using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSO.Domain.Identity;

namespace SSO.Infrastructure.Persistence.Configurations;

public class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistory>
{
    public void Configure(EntityTypeBuilder<PasswordHistory> b)
    {
        b.ToTable("PasswordHistories");
        b.HasKey(h => h.Id);

        b.Property(h => h.PasswordHash).HasMaxLength(256).IsRequired();

        b.HasOne(h => h.User)
            .WithMany(u => u.PasswordHistories)
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The no-reuse check reads the most recent N hashes for a user.
        b.HasIndex(h => new { h.UserId, h.CreatedAtUtc });
    }
}
