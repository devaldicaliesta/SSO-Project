using SSO.Domain.Auditing;

namespace SSO.Domain.Rbac;

/// <summary>
/// A navigation menu node. Menus form a tree (<see cref="ParentId"/> self-ref).
/// A node is visible to a user only if its <see cref="RequiredPermission"/> is in
/// the user's effective permission set (a null requirement = always visible).
/// The client renders the server-resolved tree; it never hardcodes links, so a
/// permission change is reflected without a client redeploy.
/// </summary>
[Auditable]
public class Menu
{
    public int Id { get; set; }

    /// <summary>Parent node id; null for a top-level item.</summary>
    public int? ParentId { get; set; }
    public Menu? Parent { get; set; }
    public ICollection<Menu> Children { get; set; } = new List<Menu>();

    /// <summary>Stable machine code (e.g. <c>master-data.shares</c>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Bootstrap-icon class (e.g. <c>bi-building</c>).</summary>
    public string? Icon { get; set; }

    /// <summary>Client route (e.g. <c>/master-data/shares</c>); null for a non-navigable group header.</summary>
    public string? Route { get; set; }

    /// <summary>Ordering among siblings.</summary>
    public int SortOrder { get; set; }

    /// <summary>Soft on/off switch without deleting the row.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The permission required to see this node; null = visible to any authenticated user.</summary>
    public int? RequiredPermissionId { get; set; }
    public Permission? RequiredPermission { get; set; }
}
