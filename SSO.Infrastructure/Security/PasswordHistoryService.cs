using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SSO.Application.Abstractions;
using SSO.Domain.Identity;
using SSO.Infrastructure.Persistence;

namespace SSO.Infrastructure.Security;

/// <summary>
/// Stores and checks recent password hashes so a user cannot reuse one of their
/// last <see cref="HistoryDepth"/> passwords.
/// </summary>
public sealed class PasswordHistoryService : IPasswordHistoryService
{
    public const int HistoryDepth = 5;

    private readonly SsoDbContext _db;
    private readonly IPasswordHasher<ApplicationUser> _hasher;

    public PasswordHistoryService(SsoDbContext db, IPasswordHasher<ApplicationUser> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<bool> IsReusedAsync(ApplicationUser user, string newPassword, CancellationToken cancellationToken = default)
    {
        if (user.Id == Guid.Empty)
            return false; // brand-new user, no history yet

        var recentHashes = await _db.PasswordHistories
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.CreatedAtUtc)
            .Take(HistoryDepth)
            .Select(h => h.PasswordHash)
            .ToListAsync(cancellationToken);

        return recentHashes.Any(hash =>
            _hasher.VerifyHashedPassword(user, hash, newPassword) != PasswordVerificationResult.Failed);
    }

    public async Task RecordCurrentAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(user.PasswordHash))
            return;

        _db.PasswordHistories.Add(new PasswordHistory
        {
            UserId = user.Id,
            PasswordHash = user.PasswordHash,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        // Trim anything older than the most recent HistoryDepth entries.
        var stale = await _db.PasswordHistories
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.CreatedAtUtc)
            .Skip(HistoryDepth)
            .ToListAsync(cancellationToken);

        if (stale.Count > 0)
            _db.PasswordHistories.RemoveRange(stale);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
