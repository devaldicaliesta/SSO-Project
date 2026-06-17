namespace SSO.Application.Abstractions;

/// <summary>
/// Ambient "who/where" context for an audited operation, resolved from the
/// current request. Kept as an abstraction so the domain/infrastructure never
/// take a direct dependency on ASP.NET's <c>HttpContext</c> (the host implements
/// <see cref="IAuditContextProvider"/> from <c>IHttpContextAccessor</c>).
/// </summary>
public sealed record AuditContext(
    Guid? ActorUserId,
    string? ActorUserName,
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent)
{
    /// <summary>Context for background/system operations with no interactive actor.</summary>
    public static readonly AuditContext System =
        new(null, "system", null, null, null);
}

/// <summary>Supplies the current <see cref="AuditContext"/> to auditing components.</summary>
public interface IAuditContextProvider
{
    AuditContext GetCurrent();
}
