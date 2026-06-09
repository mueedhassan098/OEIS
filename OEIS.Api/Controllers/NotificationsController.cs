using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEIS.Infrastructure.Data;

namespace OEIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly OeisDbContext _db;

    public NotificationsController(OeisDbContext db)
    {
        _db = db;
    }

    /// <summary>Recent company-wide activity for the notification bell.</summary>
    [HttpGet("feed")]
    public async Task<ActionResult<IEnumerable<PortalActivityFeedDto>>> GetFeed([FromQuery] int take = 40)
    {
        take = Math.Clamp(take, 1, 100);
        var rows = await _db.PortalActivities
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new PortalActivityFeedDto(
                x.PortalActivityId,
                x.Title,
                x.Message,
                x.Type,
                x.Link,
                x.CreatedAtUtc))
            .ToListAsync();

        return rows;
    }
}

public sealed record PortalActivityFeedDto(
    long Id,
    string Title,
    string Message,
    string Type,
    string? Link,
    DateTime CreatedAtUtc);
