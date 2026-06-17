using Microsoft.EntityFrameworkCore;
using SSO.Application.Abstractions;
using SSO.Domain.Enums;
using SSO.Infrastructure.Persistence;

namespace SSO.Infrastructure.Rbac;

/// <summary>
/// Resolves effective permissions from the database:
/// role permissions ∪ user grants, minus user denies (deny wins). Expired
/// time-boxed overrides are ignored.
/// </summary>
public sealed class PermissionResolver : IPermissionResolver
{
    private readonly SsoDbContext _db;

    public PermissionResolver(SsoDbContext db) => _db = db;

    public async Task<IReadOnlySet<string>> ResolveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var roleIds = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        var rolePermissions = await _db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission!.Code)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var overrides = await _db.UserPermissions
            .Where(up => up.UserId == userId && (up.ExpiresAtUtc == null || up.ExpiresAtUtc > now))
            .Select(up => new { up.Type, Code = up.Permission!.Code })
            .ToListAsync(cancellationToken);

        var effective = new HashSet<string>(rolePermissions, StringComparer.OrdinalIgnoreCase);

        foreach (var grant in overrides.Where(o => o.Type == PermissionOverrideType.Grant))
            effective.Add(grant.Code);

        // Deny always wins, applied last.
        foreach (var deny in overrides.Where(o => o.Type == PermissionOverrideType.Deny))
            effective.Remove(deny.Code);

        return effective;
    }
}
