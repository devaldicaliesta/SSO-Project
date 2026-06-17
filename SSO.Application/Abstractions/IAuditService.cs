using SSO.Domain.Auditing;

namespace SSO.Application.Abstractions;

/// <summary>
/// Records an explicit entry in the activity-monitoring trail (authentication,
/// authorization, and sensitive use-case events). The implementation fills in any
/// blank context fields (actor, correlation id, IP, user-agent, timestamp) from
/// the current request before persisting, so callers only set what they know.
/// </summary>
public interface IAuditService
{
    Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
