using SSO.Domain.Identity;

namespace SSO.Application.Abstractions;

/// <summary>
/// Enforces and records password-history (no-reuse) policy. <see cref="IsReusedAsync"/>
/// checks a candidate password against the user's recent hashes; <see cref="RecordCurrentAsync"/>
/// stores the user's current hash and trims the history to the configured depth.
/// </summary>
public interface IPasswordHistoryService
{
    Task<bool> IsReusedAsync(ApplicationUser user, string newPassword, CancellationToken cancellationToken = default);

    Task RecordCurrentAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
