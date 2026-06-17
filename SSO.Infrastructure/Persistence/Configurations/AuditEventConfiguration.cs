using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSO.Domain.Auditing;

namespace SSO.Infrastructure.Persistence.Configurations;

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("AuditEvents");
        b.HasKey(a => a.Id);

        // Store enums as strings so the trail is human-readable directly in SSMS.
        b.Property(a => a.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(a => a.Outcome).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(a => a.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.Property(a => a.Action).HasMaxLength(100).IsRequired();
        b.Property(a => a.ActorUserName).HasMaxLength(256);
        b.Property(a => a.TargetType).HasMaxLength(200);
        b.Property(a => a.TargetId).HasMaxLength(256);
        b.Property(a => a.CorrelationId).HasMaxLength(64);
        b.Property(a => a.IpAddress).HasMaxLength(64);
        b.Property(a => a.UserAgent).HasMaxLength(512);

        // Common query paths for the audit viewer / SIEM export.
        b.HasIndex(a => a.OccurredAtUtc);
        b.HasIndex(a => a.ActorUserId);
        b.HasIndex(a => new { a.Category, a.Action });

        // NOTE: the migration upgrades this table to a SQL Server 2022 append-only
        // LEDGER table (tamper-evident). See docs/ARCHITECTURE-SSO-Master-v1.md §5.2.
    }
}
