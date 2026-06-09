using Microsoft.EntityFrameworkCore;
using OEIS.Application.Services;
using OEIS.Domain.Entities;
using OEIS.Infrastructure.Data;

namespace OEIS.Infrastructure.Services;

public sealed class PortalActivityService : IPortalActivityService
{
    private readonly OeisDbContext _db;

    public PortalActivityService(OeisDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(string title, string message, string type = "Info", string? link = null, CancellationToken cancellationToken = default)
    {
        var normalizedType = string.IsNullOrWhiteSpace(type) ? "Info" : type.Trim();
        _db.PortalActivities.Add(new PortalActivity
        {
            Title = title.Trim(),
            Message = message.Trim(),
            Type = normalizedType,
            Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
