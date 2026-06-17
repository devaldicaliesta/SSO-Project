using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SSO.Application.Abstractions;
using SSO.Domain.Auditing;
using SSO.Domain.Enums;

namespace SSO.Infrastructure.Auditing;

/// <summary>
/// EF Core interceptor that writes a <see cref="AuditEvent"/> for every change to
/// an entity marked <see cref="AuditableAttribute"/>. This is the data-change feed
/// of the activity-monitoring trail: it runs inside the same transaction as the
/// change, so the audit row and the business change commit (or roll back) together.
/// Sensitive properties (password hashes, MFA secrets, security stamps) are never
/// captured in clear — only the fact that they changed.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
        "MfaSecretEncrypted", "TwoFactorRecoveryCode"
    };

    private readonly IAuditContextProvider _auditContext;

    public AuditSaveChangesInterceptor(IAuditContextProvider auditContext)
        => _auditContext = auditContext;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            AddAuditEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            AddAuditEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEvents(DbContext context)
    {
        var ctx = _auditContext.GetCurrent();
        var now = DateTimeOffset.UtcNow;

        // Snapshot the audited entries first: adding audit rows below mutates the
        // change tracker, which would otherwise invalidate the enumeration.
        var audited = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not AuditEvent)
            .Where(e => Attribute.IsDefined(e.Entity.GetType(), typeof(AuditableAttribute)))
            .ToList();

        foreach (var entry in audited)
        {
            context.Add(new AuditEvent
            {
                OccurredAtUtc = now,
                Category = AuditCategory.DataChange,
                Action = $"{entry.Entity.GetType().Name}.{entry.State}",
                Outcome = AuditOutcome.Success,
                Severity = AuditSeverity.Info,
                ActorUserId = ctx.ActorUserId,
                ActorUserName = ctx.ActorUserName,
                TargetType = entry.Entity.GetType().Name,
                TargetId = PrimaryKeyOf(entry),
                CorrelationId = ctx.CorrelationId,
                IpAddress = ctx.IpAddress,
                UserAgent = ctx.UserAgent,
                DetailsJson = BuildDetails(entry)
            });
        }
    }

    private static string? PrimaryKeyOf(EntityEntry entry)
    {
        var keyParts = entry.Properties
            .Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue?.ToString())
            .Where(v => !string.IsNullOrEmpty(v));

        var joined = string.Join(":", keyParts);
        return string.IsNullOrEmpty(joined) ? null : joined;
    }

    private static string? BuildDetails(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();

        foreach (var p in entry.Properties)
        {
            var name = p.Metadata.Name;
            var sensitive = SensitiveProperties.Contains(name);

            switch (entry.State)
            {
                case EntityState.Added:
                    changes[name] = sensitive ? "***" : p.CurrentValue;
                    break;

                case EntityState.Deleted:
                    changes[name] = sensitive ? "***" : p.OriginalValue;
                    break;

                case EntityState.Modified when p.IsModified:
                    changes[name] = sensitive
                        ? "***"
                        : new { from = p.OriginalValue, to = p.CurrentValue };
                    break;
            }
        }

        return changes.Count == 0 ? null : JsonSerializer.Serialize(changes);
    }
}
