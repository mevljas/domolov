using Domolov.Application.Homes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domolov.Infrastructure.Persistence;

/// <summary>Applies EF migrations and backfills derived columns (idempotent).</summary>
public sealed class DatabaseMigrator(
    DomolovDbContext db,
    HomeLinker linker,
    ILogger<DatabaseMigrator> logger
)
{
    private const int BatchSize = 500;

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await BackfillListingsAsync(cancellationToken);
        await BackfillHomesAsync(cancellationToken);
    }

    /// <summary>Parses numeric attributes and normalised text for Listings migrated from v1.</summary>
    private async Task BackfillListingsAsync(CancellationToken cancellationToken)
    {
        var total = 0;
        while (true)
        {
            var batch = await db
                .Listings.Where(l => l.NormalizedTitle == null)
                .OrderBy(l => l.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var listing in batch)
            {
                listing.RecomputeDerived();
            }

            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            total += batch.Count;
        }

        if (total > 0)
        {
            logger.LogInformation("Backfilled derived fields for {Count} Listings", total);
        }
    }

    /// <summary>Fills denormalised Home fields for Homes created by the v2 migration.</summary>
    private async Task BackfillHomesAsync(CancellationToken cancellationToken)
    {
        var total = 0;
        while (true)
        {
            var batch = await db
                .Homes.Where(h => h.PrimaryListingId == null)
                .OrderBy(h => h.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var home in batch)
            {
                await linker.RefreshAsync(home, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            var orphans = batch.Where(h => h.PrimaryListingId == null).Select(h => h.Id).ToList();
            if (orphans.Count > 0)
            {
                await db
                    .Homes.Where(h => orphans.Contains(h.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            db.ChangeTracker.Clear();
            total += batch.Count;
        }

        if (total > 0)
        {
            logger.LogInformation("Backfilled {Count} Homes", total);
        }
    }
}
