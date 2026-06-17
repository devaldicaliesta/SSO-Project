using SSO.Application.Abstractions;

namespace Server.Services;

/// <summary>
/// Resolves the <see cref="AuditContext"/> (actor, correlation id, IP, user-agent)
/// from the current HTTP request. This is the host's implementation of the
/// abstraction the audit interceptor/service depend on, keeping <c>HttpContext</c>
/// out of the lower layers.
/// </summary>
public sealed class HttpContextAuditContextProvider : IAuditContextProvider
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextAuditContextProvider(IHttpContextAccessor accessor) => _accessor = accessor;

    public AuditContext GetCurrent()
    {
        var http = _accessor.HttpContext;
        if (http is null)
            return AuditContext.System;

        var user = http.User;
        Guid? actorId = Guid.TryParse(user?.FindFirst("sub")?.Value, out var id) ? id : null;
        var actorName = user?.FindFirst("name")?.Value ?? user?.Identity?.Name;

        var ua = http.Request.Headers.UserAgent.ToString();

        return new AuditContext(
            ActorUserId: actorId,
            ActorUserName: actorName,
            CorrelationId: http.TraceIdentifier,
            IpAddress: http.Connection.RemoteIpAddress?.ToString(),
            UserAgent: string.IsNullOrWhiteSpace(ua) ? null : ua);
    }
}
