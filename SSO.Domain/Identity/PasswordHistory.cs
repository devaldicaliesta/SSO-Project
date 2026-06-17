namespace SSO.Domain.Identity;

/// <summary>
/// A single historical password hash for a user. The password-history validator
/// rejects a new password whose hash matches any of the most recent N entries,
/// preventing password reuse (NIST / OJK-defensible password policy).
/// </summary>
public class PasswordHistory
{
    public long Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>The PBKDF2 hash that was in effect (never the plaintext password).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>When this hash became the user's password.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
