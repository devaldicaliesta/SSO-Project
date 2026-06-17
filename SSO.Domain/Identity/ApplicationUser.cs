using Microsoft.AspNetCore.Identity;
using SSO.Domain.Auditing;
using SSO.Domain.Enums;
using SSO.Domain.Rbac;

namespace SSO.Domain.Identity;

/// <summary>
/// The master SSO user. Extends ASP.NET Core Identity (which already provides
/// PBKDF2 password hashing, <c>LockoutEnd</c>/<c>AccessFailedCount</c>,
/// <c>TwoFactorEnabled</c> and the <c>SecurityStamp</c> used to invalidate
/// sessions when credentials/permissions change). GUID keys are non-enumerable
/// and safe to surface in URLs/tokens.
/// </summary>
[Auditable]
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Display name shown in the UI and recorded in the audit trail.</summary>
    [ProtectedPersonalData]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Account lifecycle state (Joiner-Mover-Leaver). Disabled users cannot sign in.</summary>
    public UserStatus Status { get; set; } = UserStatus.Pending;

    /// <summary>
    /// When set, the user is forced to change the password at next login
    /// (admin-provisioned temporary password, or post-reset).
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>UTC timestamp of the last successful password change (for max-age policy).</summary>
    public DateTimeOffset? PasswordChangedAtUtc { get; set; }

    /// <summary>UTC timestamp of the last successful interactive login.</summary>
    public DateTimeOffset? LastLoginAtUtc { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Enrolled second-factor mechanism.</summary>
    public MfaType MfaType { get; set; } = MfaType.None;

    /// <summary>
    /// Per-user TOTP secret, encrypted at rest via the ASP.NET Core Data Protection
    /// API. Never store the raw Base32 secret. Null until the user enrolls.
    /// </summary>
    [ProtectedPersonalData]
    public string? MfaSecretEncrypted { get; set; }

    // ----- Navigation -----

    /// <summary>Per-user permission overrides (grant/deny) on top of role-derived permissions.</summary>
    public ICollection<UserPermission> PermissionOverrides { get; set; } = new List<UserPermission>();

    /// <summary>Recent password hashes, used to enforce password-history (no-reuse) policy.</summary>
    public ICollection<PasswordHistory> PasswordHistories { get; set; } = new List<PasswordHistory>();
}
