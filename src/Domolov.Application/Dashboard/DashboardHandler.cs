using Domolov.Application.Abstractions;
using Domolov.Application.Homes;
using Domolov.Application.Scans;
using Domolov.Application.Watches;
using Domolov.Domain.Homes;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Application.Dashboard;

/// <summary>Headline numbers for the dashboard.</summary>
public sealed record DashboardStats(
    int ActiveWatches,
    int PausedWatches,
    int OnMarketHomes,
    int UnseenHomes,
    int NewHomes24h,
    int PriceDrops7d,
    int Reposts7d,
    int PossibleMatches,
    int BookmarkedHomes
);

/// <summary>Everything the dashboard shows.</summary>
public sealed record DashboardResponse(
    DashboardStats Stats,
    IReadOnlyList<HomeSummaryResponse> Unseen,
    IReadOnlyList<HomeSummaryResponse> PriceDrops,
    IReadOnlyList<WatchResponse> Watches,
    IReadOnlyList<ScanRunResponse> ActiveScans
);

/// <summary>Builds the dashboard.</summary>
public sealed class GetDashboardHandler(
    IAppDbContext db,
    ListWatchesHandler listWatches,
    TimeProvider clock
)
{
    private const int StripSize = 12;

    public async Task<DashboardResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var dayAgo = now.AddDays(-1);
        var weekAgo = now.AddDays(-7);
        var visible = db
            .Homes.AsNoTracking()
            .Where(h => h.DismissedAt == null && h.PrimaryListingId != null);

        var stats = new DashboardStats(
            await db.Watches.CountAsync(w => !w.IsPaused, cancellationToken),
            await db.Watches.CountAsync(w => w.IsPaused, cancellationToken),
            await visible.CountAsync(h => h.OffMarketAt == null, cancellationToken),
            await visible.CountAsync(
                h => h.SeenAt == null && h.OffMarketAt == null,
                cancellationToken
            ),
            await visible.CountAsync(h => h.FirstSeenAt >= dayAgo, cancellationToken),
            await visible.CountAsync(
                h => h.PriceChangedAt >= weekAgo && h.CurrentPrice < h.PreviousPrice,
                cancellationToken
            ),
            await db.HomeMatches.CountAsync(
                m => m.CreatedAt >= weekAgo && m.State == HomeMatchState.AutoLinked,
                cancellationToken
            ),
            await db.HomeMatches.CountAsync(
                m => m.State == HomeMatchState.Possible,
                cancellationToken
            ),
            await visible.CountAsync(h => h.Bookmark != null, cancellationToken)
        );

        var unseen = await HomeProjection
            .Summaries(
                db,
                visible
                    .Where(h => h.SeenAt == null && h.OffMarketAt == null)
                    .OrderByDescending(h => h.PriceChangedAt ?? h.FirstSeenAt)
                    .Take(StripSize)
            )
            .ToListAsync(cancellationToken);
        var drops = await HomeProjection
            .Summaries(
                db,
                visible
                    .Where(h => h.OffMarketAt == null && h.CurrentPrice < h.PreviousPrice)
                    .OrderByDescending(h => h.PriceChangedAt)
                    .Take(StripSize)
            )
            .ToListAsync(cancellationToken);
        var active = await ScanRunProjection
            .Project(
                db,
                db.ScanRuns.AsNoTracking()
                    .Where(r =>
                        r.Status == Domain.Scans.ScanRunStatus.Queued
                        || r.Status == Domain.Scans.ScanRunStatus.Running
                    )
                    .OrderBy(r => r.QueuedAt)
            )
            .ToListAsync(cancellationToken);

        return new DashboardResponse(
            stats,
            unseen,
            drops,
            await listWatches.HandleAsync(cancellationToken),
            active
        );
    }
}
