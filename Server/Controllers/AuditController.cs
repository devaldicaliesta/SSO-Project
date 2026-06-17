using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared;
using SSO.Infrastructure.Persistence;

namespace Server.Controllers;

/// <summary>
/// GET /api/audit — paged, read-only view of the activity-monitoring trail.
/// Requires the <c>admin.audit.view</c> permission.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "perm:admin.audit.view")]
public class AuditController : ControllerBase
{
    private readonly SsoDbContext _db;

    public AuditController(SsoDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditEventDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.AuditEvents.AsNoTracking().OrderByDescending(a => a.OccurredAtUtc);

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(a => new AuditEventDto
        {
            Id = a.Id,
            OccurredAtUtc = a.OccurredAtUtc,
            Category = a.Category.ToString(),
            Action = a.Action,
            Outcome = a.Outcome.ToString(),
            Severity = a.Severity.ToString(),
            ActorUserName = a.ActorUserName,
            TargetType = a.TargetType,
            TargetId = a.TargetId,
            IpAddress = a.IpAddress,
        }).ToList();

        return Ok(new PagedResult<AuditEventDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
        });
    }
}
