using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Application.Homes;

/// <summary>Moves Listings between Homes, applying merge rules and keeping Homes consistent.</summary>
public sealed class HomeLinker(IAppDbContext db, TimeProvider clock)
{
    /// <summary>
    /// Moves the Listing into <paramref name="target"/>. When its previous Home has no other
    /// Listings, the target absorbs it (Bookmark, Dismissal) and the old Home is deleted.
    /// Saves and refreshes both Homes.
    /// </summary>
    public async Task MoveListingAsync(
        Listing listing,
        Home target,
        CancellationToken cancellationToken
    )
    {
        var sourceId = listing.HomeId;
        if (sourceId == target.Id)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var source = await db
            .Homes.Include(h => h.Bookmark)
            .FirstAsync(h => h.Id == sourceId, cancellationToken);
        var othersInSource = await db.Listings.CountAsync(
            l => l.HomeId == sourceId && l.Id != listing.Id,
            cancellationToken
        );

        listing.AssignHome(target.Id);
        if (othersInSource == 0)
        {
            target.Absorb(source, now);
            var matches = await db
                .HomeMatches.Where(m => m.HomeId == sourceId)
                .ToListAsync(cancellationToken);
            foreach (var match in matches)
            {
                match.RetargetHome(target.Id);
            }

            db.Homes.Remove(source);
        }

        await db.SaveChangesAsync(cancellationToken);
        await RefreshAsync(target, cancellationToken);
        if (othersInSource > 0)
        {
            await RefreshAsync(source, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Splits a Listing off into a new Home without Bookmark or Dismissal and remembers the pair
    /// as different homes so it is never suggested again.
    /// </summary>
    public async Task<Home> UnlinkAsync(
        Listing listing,
        Home from,
        CancellationToken cancellationToken
    )
    {
        var others = await db.Listings.CountAsync(
            l => l.HomeId == from.Id && l.Id != listing.Id,
            cancellationToken
        );
        if (others == 0)
        {
            throw new ConflictException(
                "This is the only ad of the Home; there is nothing to split."
            );
        }

        var now = clock.GetUtcNow();
        var fresh = new Home(now);
        db.Homes.Add(fresh);
        listing.AssignHome(fresh.Id);
        db.HomeMatches.Add(HomeMatch.RejectedPair(listing.Id, from.Id, now));
        await db
            .HomeMatches.Where(m =>
                m.ListingId == listing.Id
                && m.HomeId == from.Id
                && m.State != HomeMatchState.Rejected
            )
            .ExecuteDeleteAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await RefreshAsync(fresh, cancellationToken);
        await RefreshAsync(from, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return fresh;
    }

    public async Task<HomePriceChange> RefreshAsync(Home home, CancellationToken cancellationToken)
    {
        var listings = await db
            .Listings.Where(l => l.HomeId == home.Id)
            .ToListAsync(cancellationToken);
        return home.Refresh(listings, clock.GetUtcNow());
    }
}
