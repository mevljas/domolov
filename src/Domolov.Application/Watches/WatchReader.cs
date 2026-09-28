using Domolov.Application.Abstractions;
using Domolov.Application.Notifications;
using Domolov.Application.Options;
using Domolov.Application.Scans;
using Domolov.Domain.Watches;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Domolov.Application.Watches;

/// <summary>Loads Watches with their latest ScanRun and listing counts and maps them.</summary>
public sealed class WatchReader(
    IAppDbContext db,
    IOptions<DomolovOptions> options,
    TimeProvider clock
)
{
    public async Task<IReadOnlyList<WatchResponse>> ReadAsync(
        IQueryable<Watch> query,
        CancellationToken cancellationToken
    )
    {
        var watches = await query
            .AsNoTracking()
            .Include(w => w.NotificationRoutes)
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken);
        if (watches.Count == 0)
        {
            return [];
        }

        var ids = watches.Select(w => w.Id).ToList();
        var lastRuns = await db
            .ScanRuns.AsNoTracking()
            .Where(r => ids.Contains(r.WatchId))
            .GroupBy(r => r.WatchId)
            .Select(g => g.OrderByDescending(r => r.QueuedAt).First())
            .ToListAsync(cancellationToken);
        var counts = await db
            .WatchSightings.AsNoTracking()
            .Where(s => ids.Contains(s.WatchId) && !s.IsStale)
            .GroupBy(s => s.WatchId)
            .Select(g => new { WatchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WatchId, x => x.Count, cancellationToken);

        var runsByWatch = lastRuns.ToDictionary(r => r.WatchId);
        var now = clock.GetUtcNow();
        var tz = options.Value.ResolveTimeZone();
        return watches
            .Select(w =>
            {
                runsByWatch.TryGetValue(w.Id, out var run);
                var lastScan = run is null ? null : ScanRunMapping.ToResponse(run, w.Name, 0);
                return Map(w, lastScan, counts.GetValueOrDefault(w.Id), now, tz);
            })
            .ToList();
    }

    public static WatchResponse Map(
        Watch watch,
        ScanRunResponse? lastScan,
        int listingCount,
        DateTimeOffset now,
        TimeZoneInfo timeZone
    ) =>
        new(
            watch.Id,
            watch.Name,
            watch.ProviderId,
            watch.SearchUrl,
            watch.Cron,
            watch.IsPaused,
            watch.HasCompletedBaseline,
            watch.CreatedAt,
            watch.LastScannedAt,
            NextRunAt(watch, now, timeZone),
            watch.CloudflareStrikeCount,
            watch.CloudflareBlockedUntil,
            listingCount,
            lastScan,
            watch
                .NotificationRoutes.OrderBy(r => r.Channel)
                .ThenBy(r => r.Destination)
                .Select(NotificationRouteMapping.ToResponse)
                .ToList(),
            watch.Version
        );

    public static DateTimeOffset? NextRunAt(Watch watch, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        if (watch.IsPaused)
        {
            return null;
        }

        var next = WatchSchedule.GetNextRunAt(
            watch.Id,
            watch.Cron,
            watch.LastScannedAt,
            now,
            timeZone
        );
        if (next is null)
        {
            return null;
        }

        if (watch.CloudflareBlockedUntil is { } blocked && blocked > next)
        {
            next = blocked;
        }

        return next < now ? now : next;
    }
}
