using SSO.Domain.Auditing;
using SSO.Domain.Enums;
using SSO.Domain.Identity;

namespace SSO.Domain.Rbac;

/// <summary>
/// A per-user override on top of role-derived permissions. A
/// <see cref="PermissionOverrideType.Deny"/> always wins over any grant, so it
/// can be used to surgically revoke a single capability from a user without
/// removing them from a role (supports Segregation of Duties enforcement).
/// </summary>
[Auditable]
public class UserPermission
{
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public int PermissionId { get; set; }
    public Permission? Permission { get; set; }

    public PermissionOverrideType Type { get; set; } = PermissionOverrideType.Grant;

    /// <summary>Optional reason (for audit/recertification: "temporary access for incident #123").</summary>
    public string? Reason { get; set; }

    /// <summary>Optional expiry for time-boxed (just-in-time) grants.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}
