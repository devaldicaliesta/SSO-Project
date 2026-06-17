namespace Shared;

/// <summary>
/// A single navigation node returned by <c>GET /api/menu</c>. The server resolves
/// the tree against the caller's effective permissions, so the client only ever
/// receives the nodes it is allowed to see (no client-side permission logic, no
/// hidden-but-present links).
/// </summary>
public class MenuItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }

    /// <summary>Client route, or null for a non-navigable group header.</summary>
    public string? Route { get; set; }

    public List<MenuItemDto> Children { get; set; } = new();
}
