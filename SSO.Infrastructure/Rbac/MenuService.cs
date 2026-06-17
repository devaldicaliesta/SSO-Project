using Microsoft.EntityFrameworkCore;
using Shared;
using SSO.Application.Abstractions;
using SSO.Domain.Rbac;
using SSO.Infrastructure.Persistence;

namespace SSO.Infrastructure.Rbac;

/// <summary>
/// Loads the active menu tree and returns only the nodes whose required permission
/// the caller holds. Group headers (no route) that end up with no visible children
/// are pruned so the UI never shows an empty section.
/// </summary>
public sealed class MenuService : IMenuService
{
    private readonly SsoDbContext _db;

    public MenuService(SsoDbContext db) => _db = db;

    public async Task<IReadOnlyList<MenuItemDto>> GetMenuAsync(
        IReadOnlySet<string> permissions, CancellationToken cancellationToken = default)
    {
        var menus = await _db.Menus
            .Where(m => m.IsActive)
            .Include(m => m.RequiredPermission)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

        var visible = menus
            .Where(m => m.RequiredPermission == null || permissions.Contains(m.RequiredPermission.Code))
            .ToList();

        MenuItemDto Map(Menu m) => new()
        {
            Id = m.Id,
            Code = m.Code,
            Label = m.Label,
            Icon = m.Icon,
            Route = m.Route,
            Children = visible.Where(c => c.ParentId == m.Id).Select(Map).ToList()
        };

        return visible
            .Where(m => m.ParentId == null)
            .Select(Map)
            // Keep navigable roots, and group headers that have at least one visible child.
            .Where(node => node.Route != null || node.Children.Count > 0)
            .ToList();
    }
}
