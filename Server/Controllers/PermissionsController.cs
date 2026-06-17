using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared;
using SSO.Infrastructure.Persistence;

namespace Server.Controllers;

/// <summary>
/// GET /api/permissions — the permission catalogue, for the role editor. Requires
/// <c>admin.roles.manage</c>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "perm:admin.roles.manage")]
public class PermissionsController : ControllerBase
{
    private readonly SsoDbContext _db;

    public PermissionsController(SsoDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> List(CancellationToken ct)
    {
        var permissions = await _db.Permissions
            .OrderBy(p => p.Category).ThenBy(p => p.Code)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Category = p.Category
            })
            .ToListAsync(ct);

        return Ok(permissions);
    }
}
