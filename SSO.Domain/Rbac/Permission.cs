using SSO.Domain.Auditing;

namespace SSO.Domain.Rbac;

/// <summary>
/// The atomic unit of authorization. Every protected API endpoint and every menu
/// is gated by a permission <see cref="Code"/> (e.g. <c>shares.read</c>,
/// <c>coa.write</c>, <c>users.manage</c>) rather than by a role name, so access
/// can be reorganized by editing role membership without touching code.
/// </summary>
[Auditable]
public class Permission
{
    public int Id { get; set; }

    /// <summary>
    /// Stable machine code in <c>resource.action</c> form. This is what
    /// <c>[Authorize(Policy = "perm:shares.read")]</c> checks. Immutable once issued.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable name for the admin console.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Grouping for the admin UI (e.g. "Master Data", "Reports", "Administration").</summary>
    public string? Category { get; set; }

    // ----- Navigation -----
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    public ICollection<Menu> Menus { get; set; } = new List<Menu>();
}
