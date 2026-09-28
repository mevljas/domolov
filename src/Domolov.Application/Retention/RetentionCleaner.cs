using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Application.Homes;
using Domolov.Application.Options;
using Domolov.Domain.Retention;
using Domolov.Domain.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domolov.Application.Retention;

/// <summary>Deletes data past its retention period in small batches.</summary>
public sealed class RetentionCleaner(
    IAppDbContext db,
    HomeLinker linker,
    IOptions<RetentionOptions> options,
    TimeProvider clock,
    ILogger<RetentionCleaner> logger
)
{
    private const int BatchSize = 500;

    public async Task<CleanupRun> RunAsync(
        bool isManual,
        long? browserProfileBytes,
        CancellationToken cancellationToken
    )
    {
        var run = new CleanupRun(isManual, clock.GetUtcNow());
        db.CleanupRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var (listings, homes) = await PurgeListingsAsync(cancellationToken);
            var counts = new CleanupCounts(
                listings,
                homes,
                await PurgeScanRunsAsync(cancellationToken),
                await PurgeArtifactsAsync(cancellationToken),
                await PurgeSessionsAsync(cancellationToken)
            );
            run.Finish(counts, browserProfileBytes, clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            Record("listings", counts.Listings);
            Record("homes", counts.Homes);
            Record("scan_runs", counts.ScanRuns);
            Record("artifacts", counts.Artifacts);
            Record("sessions", counts.Sessions);
            logger.LogInformation(
                "Cleanup removed {Listings} listings, {Homes} homes, {ScanRuns} scan runs, {Artifacts} artifacts, {Sessions} sessions",
                counts.Listings,
                counts.Homes,
                counts.ScanRuns,
                counts.Artifacts,
                counts.Sessions
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Cleanup failed");
            run.Fail(ex.Message, clock.GetUtcNow());
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return run;
    }

    /// <summary>
    /// Purges Delisted Listings and Listings no Watch sights under its current URL any more,
    /// past their retention period, unless their Home is Bookmarked. Empty Homes are deleted and
    /// Homes that keep other Listings are refreshed.
    /// </summary>
    internal async Task<(int Listings, int Homes)> PurgeListingsAsync(
        CancellationToken cancellationToken
    )
    {
        var now = clock.GetUtcNow();
        var delistedCutoff = now.AddDays(-options.Value.DelistedDays);
        var orphanCutoff = now.AddDays(-options.Value.OrphanListingDays);

        var listingsDeleted = 0;
        var affectedHomes = new HashSet<Guid>();
        while (true)
        {
            var batch = await db
                .Listings.Where(l => !db.Bookmarks.Any(b => b.HomeId == l.HomeId))
                .Where(l =>
                    (l.DelistedAt != null && l.DelistedAt < delistedCutoff)
                    || (
                        l.LastSeenAt < orphanCutoff
                        && !db.WatchSightings.Any(s => s.ListingId == l.Id && !s.IsStale)
                    )
                )
                .OrderBy(l => l.Id)
                .Select(l => new { l.Id, l.HomeId })
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            var ids = batch.Select(b => b.Id).ToList();
            affectedHomes.UnionWith(batch.Select(b => b.HomeId));
            listingsDeleted += await db
                .Listings.Where(l => ids.Contains(l.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (affectedHomes.Count == 0)
        {
            return (0, 0);
        }

        var homeIds = affectedHomes.ToList();
        var homesDeleted = await db
            .Homes.Where(h => homeIds.Contains(h.Id) && !db.Listings.Any(l => l.HomeId == h.Id))
            .ExecuteDeleteAsync(cancellationToken);
        var remaining = await db
            .Homes.Where(h => homeIds.Contains(h.Id))
            .ToListAsync(cancellationToken);
        foreach (var home in remaining)
        {
            await linker.RefreshAsync(home, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (listingsDeleted, homesDeleted);
    }

    /// <summary>Deletes finished ScanRuns past retention, always keeping the latest N per Watch.</summary>
    internal async Task<int> PurgeScanRunsAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.ScanRunDays);
        var keep = options.Value.ScanRunKeepPerWatch;
        var watchIds = await db
            .ScanRuns.Select(r => r.WatchId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var deleted = 0;
        foreach (var watchId in watchIds)
        {
            var keepIds = await db
                .ScanRuns.Where(r => r.WatchId == watchId)
                .OrderByDescending(r => r.QueuedAt)
                .Take(keep)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);
            deleted += await db
                .ScanRuns.Where(r =>
                    r.WatchId == watchId
                    && r.QueuedAt < cutoff
                    && !keepIds.Contains(r.Id)
                    && r.Status != ScanRunStatus.Queued
                    && r.Status != ScanRunStatus.Running
                )
                .ExecuteDeleteAsync(cancellationToken);
        }

        return deleted;
    }

    internal async Task<int> PurgeArtifactsAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.ArtifactDays);
        return await db
            .ScanRunArtifacts.Where(a => a.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    internal async Task<int> PurgeSessionsAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.SessionDays);
        return await db
            .OperatorSessions.Where(s =>
                (s.RevokedAt != null && s.RevokedAt < cutoff) || s.ExpiresAt < cutoff
            )
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static void Record(string kind, int count) =>
        DomolovTelemetry.CleanupDeleted.Add(count, new KeyValuePair<string, object?>("kind", kind));
}
