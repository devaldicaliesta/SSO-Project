using SSO.Application.Abstractions;
using SSO.Domain.Auditing;
using SSO.Infrastructure.Persistence;

namespace SSO.Infrastructure.Auditing;

/// <summary>
/// Writes explicit audit entries (authentication, authorization, sensitive
/// use-cases). Blank context fields are filled from the current request before the
/// row is persisted. Note: <see cref="AuditEvent"/> is not <c>[Auditable]</c>, so
/// saving here does not recurse through the change interceptor.
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly SsoDbContext _db;
    private readonly IAuditContextProvider _context;

    public AuditService(SsoDbContext db, IAuditContextProvider context)
    {
        _db = db;
        _context = context;
    }

    public async Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        var ctx = _context.GetCurrent();

        if (auditEvent.OccurredAtUtc == default)
            auditEvent.OccurredAtUtc = DateTimeOffset.UtcNow;

        auditEvent.ActorUserId ??= ctx.ActorUserId;
        auditEvent.ActorUserName ??= ctx.ActorUserName;
        auditEvent.CorrelationId ??= ctx.CorrelationId;
        auditEvent.IpAddress ??= ctx.IpAddress;
        auditEvent.UserAgent ??= ctx.UserAgent;

        _db.AuditEvents.Add(auditEvent);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
