using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared;
using SSO.Application.Abstractions;

namespace Server.Controllers;

/// <summary>
/// GET /api/menu — the navigation tree the current user is allowed to see. The
/// server filters by the caller's <c>permission</c> claims, so the client renders
/// exactly what it receives (no client-side permission logic).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MenuController : ControllerBase
{
    private readonly IMenuService _menu;

    public MenuController(IMenuService menu) => _menu = menu;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MenuItemDto>>> Get(CancellationToken cancellationToken)
    {
        var permissions = User.FindAll("permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var menu = await _menu.GetMenuAsync(permissions, cancellationToken);
        return Ok(menu);
    }
}
