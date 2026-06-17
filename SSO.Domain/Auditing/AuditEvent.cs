using SSO.Domain.Enums;

namespace SSO.Domain.Auditing;

/// <summary>
/// One immutable record in the activity-monitoring trail. The table is created as
/// a SQL Server 2022 append-only <b>ledger table</b>, so any insert/update/delete
/// tampering is cryptographically detectable and provable to an auditor.
/// Rows are written by the audit interceptor (data changes), the IdentityServer
/// event sink (authentication), the authorization failure handler (denials), and
/// explicit <c>IAuditService.RecordAsync</c> calls (sensitive use-cases).
/// </summary>
public class AuditEvent
{
    public long Id { get; set; }

    /// <summary>When the event occurred (server UTC).</summary>
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AuditCategory Category { get; set; }

    /// <summary>Dotted action code, e.g. <c>Login.Failed</c>, <c>Role.Assigned</c>, <c>Password.Changed</c>.</summary>
    public string Action { get; set; } = string.Empty;

    public AuditOutcome Outcome { get; set; }
    public AuditSeverity Severity { get; set; } = AuditSeverity.Info;

    // ----- Actor (who) -----

    /// <summary>Null for pre-authentication events (e.g. a failed login with an unknown username).</summary>
    public Guid? ActorUserId { get; set; }
    public string? ActorUserName { get; set; }

    // ----- Target (what was acted upon) -----
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }

    /// <summary>Correlates all events emitted while handling one HTTP request.</summary>
    public string? CorrelationId { get; set; }

    // ----- Request context -----
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>
    /// Structured extra context as JSON (e.g. changed fields, denied permission code).
    /// Must never contain secrets or clear-text PII.
    /// </summary>
    public string? DetailsJson { get; set; }
}
