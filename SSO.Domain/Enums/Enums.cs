namespace SSO.Domain.Enums;

/// <summary>Lifecycle state of a user account (orthogonal to Identity lockout).</summary>
public enum UserStatus
{
    /// <summary>Active and allowed to sign in.</summary>
    Active = 0,

    /// <summary>Administratively disabled (Leaver / suspended). Cannot sign in.</summary>
    Disabled = 1,

    /// <summary>Created but not yet activated (pending first login / enrollment).</summary>
    Pending = 2
}

/// <summary>Second-factor mechanism enrolled for the user.</summary>
public enum MfaType
{
    /// <summary>No second factor enrolled.</summary>
    None = 0,

    /// <summary>RFC 6238 time-based one-time password (authenticator app).</summary>
    Totp = 1,

    /// <summary>FIDO2 / WebAuthn security key (reserved for privileged accounts, later phase).</summary>
    Fido2 = 2
}

/// <summary>
/// Direction of a per-user permission override. A <see cref="Deny"/> always wins
/// over any grant inherited from a role (deny-by-default, least privilege).
/// </summary>
public enum PermissionOverrideType
{
    Grant = 0,
    Deny = 1
}

/// <summary>High-level classification of an audited event (drives SIEM routing).</summary>
public enum AuditCategory
{
    Authentication = 0,
    Authorization = 1,
    UserManagement = 2,
    DataChange = 3,
    Security = 4,
    Configuration = 5
}

/// <summary>Whether the audited action succeeded.</summary>
public enum AuditOutcome
{
    Success = 0,
    Failure = 1
}

/// <summary>Severity of an audited event (drives alerting thresholds).</summary>
public enum AuditSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}
