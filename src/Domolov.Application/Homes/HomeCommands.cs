using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Domain.Homes;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Application.Homes;

/// <summary>Loads a tracked Home with its Bookmark.</summary>
internal static class HomeLoader
{
    public static async Task<Home> LoadAsync(
        IAppDbContext db,
        Guid id,
        CancellationToken cancellationToken
    ) =>
        await db
            .Homes.Include(h => h.Bookmark)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
        ?? throw new NotFoundException("Home", id);
}

/// <summary>Creates or updates the Home's Bookmark.</summary>
public sealed class SetBookmarkHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task<BookmarkResponse> HandleAsync(
        Guid homeId,
        SetBookmarkRequest request,
        CancellationToken cancellationToken
    )
    {
        var home = await HomeLoader.LoadAsync(db, homeId, cancellationToken);
        var bookmark = home.SetBookmark(request.Stage, request.Note, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return new BookmarkResponse(
            bookmark.Stage,
            bookmark.Note,
            bookmark.CreatedAt,
            bookmark.UpdatedAt
        );
    }
}

/// <summary>Removes the Home's Bookmark.</summary>
public sealed class RemoveBookmarkHandler(IAppDbContext db)
{
    public async Task HandleAsync(Guid homeId, CancellationToken cancellationToken)
    {
        var home = await HomeLoader.LoadAsync(db, homeId, cancellationToken);
        home.RemoveBookmark();
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Dismisses a Home: hidden from feeds, never notified, Bookmark removed.</summary>
public sealed class DismissHomeHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task HandleAsync(Guid homeId, CancellationToken cancellationToken)
    {
        var home = await HomeLoader.LoadAsync(db, homeId, cancellationToken);
        home.Dismiss(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Undoes a dismissal.</summary>
public sealed class RestoreHomeHandler(IAppDbContext db)
{
    public async Task HandleAsync(Guid homeId, CancellationToken cancellationToken)
    {
        var home = await HomeLoader.LoadAsync(db, homeId, cancellationToken);
        home.Restore();
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Marks one Home seen.</summary>
public sealed class MarkHomeSeenHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task HandleAsync(Guid homeId, CancellationToken cancellationToken)
    {
        var home = await HomeLoader.LoadAsync(db, homeId, cancellationToken);
        home.MarkSeen(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Marks every Unseen Home first seen before a point in time as seen.</summary>
public sealed class MarkAllHomesSeenHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task<CountResponse> HandleAsync(
        MarkAllSeenRequest request,
        CancellationToken cancellationToken
    )
    {
        var now = clock.GetUtcNow();
        var before = request.Before ?? now;
        var count = await db
            .Homes.Where(h => h.SeenAt == null && h.LastSeenAt <= before)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.SeenAt, now), cancellationToken);
        return new CountResponse(count);
    }
}

/// <summary>Manually links an ad to a Home.</summary>
public sealed class LinkListingHandler(
    IAppDbContext db,
    HomeLinker linker,
    TimeProvider clock,
    GetHomeHandler getHome
)
{
    public async Task<HomeDetailResponse> HandleAsync(
        Guid homeId,
        LinkListingRequest request,
        CancellationToken cancellationToken
    )
    {
        var home = await HomeLoader.LoadAsync(db, homeId, cancellationToken);
        var listing =
            await db.Listings.FirstOrDefaultAsync(l => l.Id == request.ListingId, cancellationToken)
            ?? throw new NotFoundException("Listing", request.ListingId);
        if (listing.HomeId == homeId)
        {
            return await getHome.HandleAsync(homeId, cancellationToken);
        }

        await db
            .HomeMatches.Where(m => m.ListingId == listing.Id && m.HomeId == homeId)
            .ExecuteDeleteAsync(cancellationToken);
        db.HomeMatches.Add(HomeMatch.ManualLink(listing.Id, homeId, clock.GetUtcNow()));
        await linker.MoveListingAsync(listing, home, cancellationToken);
        return await getHome.HandleAsync(homeId, cancellationToken);
    }
}

/// <summary>Splits an ad off a Home ("not the same home").</summary>
public sealed class UnlinkListingHandler(IAppDbContext db, HomeLinker linker)
{
    public async Task<HomeSummaryResponse> HandleAsync(
        Guid homeId,
        Guid listingId,
        CancellationToken cancellationToken
    )
    {
        var home = await HomeLoader.LoadAsync(db, homeId, cancellationToken);
        var listing =
            await db.Listings.FirstOrDefaultAsync(
                l => l.Id == listingId && l.HomeId == homeId,
                cancellationToken
            ) ?? throw new NotFoundException("Listing", listingId);
        var fresh = await linker.UnlinkAsync(listing, home, cancellationToken);
        return await HomeProjection
            .Summaries(db, db.Homes.AsNoTracking().Where(h => h.Id == fresh.Id))
            .FirstAsync(cancellationToken);
    }
}

/// <summary>Confirms or rejects a Possible match.</summary>
public sealed class ReviewMatchHandler(IAppDbContext db, HomeLinker linker, TimeProvider clock)
{
    public async Task<HomeMatchResponse?> HandleAsync(
        Guid matchId,
        ReviewMatchRequest request,
        CancellationToken cancellationToken
    )
    {
        var match =
            await db.HomeMatches.FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken)
            ?? throw new NotFoundException("HomeMatch", matchId);
        var now = clock.GetUtcNow();
        switch (request.State)
        {
            case HomeMatchState.Confirmed:
                match.Confirm(now);
                var listing = await db.Listings.FirstAsync(
                    l => l.Id == match.ListingId,
                    cancellationToken
                );
                var home = await HomeLoader.LoadAsync(db, match.HomeId, cancellationToken);
                await linker.MoveListingAsync(listing, home, cancellationToken);
                await db
                    .HomeMatches.Where(m =>
                        m.ListingId == match.ListingId
                        && m.Id != match.Id
                        && m.State == HomeMatchState.Possible
                    )
                    .ExecuteDeleteAsync(cancellationToken);
                break;
            case HomeMatchState.Rejected:
                match.Reject(now);
                await db.SaveChangesAsync(cancellationToken);
                break;
            default:
                throw new Domain.Common.DomainRuleException(
                    "state",
                    "State must be confirmed or rejected."
                );
        }

        var result = await HomeMatchReader.ReadAsync(
            db,
            db.HomeMatches.Where(m => m.Id == matchId),
            cancellationToken
        );
        return result.FirstOrDefault();
    }
}
