using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Domain.Homes;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Application.Homes;

/// <summary>The Homes feed with filters, sort and paging, all evaluated in SQL.</summary>
public sealed class SearchHomesHandler(IAppDbContext db)
{
    public async Task<PagedResponse<HomeSummaryResponse>> HandleAsync(
        HomeSearchQuery query,
        CancellationToken cancellationToken
    )
    {
        var (page, size, skip) = Paging.Normalize(query.Page, query.PageSize);
        var homes = Filter(db, query);
        var total = await homes.CountAsync(cancellationToken);
        var items = await HomeProjection
            .Summaries(db, Sort(homes, query.Sort ?? HomeSort.Newest).Skip(skip).Take(size))
            .ToListAsync(cancellationToken);
        return new PagedResponse<HomeSummaryResponse>(items, total, page, size);
    }

    internal static IQueryable<Home> Filter(IAppDbContext db, HomeSearchQuery q)
    {
        var homes = db.Homes.AsNoTracking().Where(h => h.PrimaryListingId != null);

        homes = (q.Dismissed ?? DismissedFilter.Exclude) switch
        {
            DismissedFilter.Include => homes,
            DismissedFilter.Only => homes.Where(h => h.DismissedAt != null),
            _ => homes.Where(h => h.DismissedAt == null),
        };

        homes = (q.Status ?? MarketStatus.OnMarket) switch
        {
            MarketStatus.OffMarket => homes.Where(h => h.OffMarketAt != null),
            MarketStatus.All => homes,
            _ => homes.Where(h => h.OffMarketAt == null),
        };

        if (q.WatchId is Guid watchId)
        {
            homes = homes.Where(h =>
                db.Listings.Any(l =>
                    l.HomeId == h.Id
                    && db.WatchSightings.Any(s =>
                        s.ListingId == l.Id && s.WatchId == watchId && !s.IsStale
                    )
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = q.Q.Trim().ToLower();
            homes = homes.Where(h =>
                h.Title.ToLower().Contains(term)
                || (h.Location != null && h.Location.ToLower().Contains(term))
            );
        }

        if (q.Bookmarked is true)
        {
            homes = homes.Where(h => h.Bookmark != null);
        }
        else if (q.Bookmarked is false)
        {
            homes = homes.Where(h => h.Bookmark == null);
        }

        if (q.Stage is BookmarkStage stage)
        {
            homes = homes.Where(h => h.Bookmark != null && h.Bookmark.Stage == stage);
        }

        if (q.Unseen is true)
        {
            homes = homes.Where(h => h.SeenAt == null);
        }

        if (q.Reposted is true)
        {
            homes = homes.Where(h => h.RepostCount > 0);
        }

        if (q.HasDuplicates is true)
        {
            homes = homes.Where(h => h.ListingCount - 1 - h.RepostCount > 0);
        }

        if (q.MinPrice is decimal minPrice)
        {
            homes = homes.Where(h => h.CurrentPrice >= minPrice);
        }

        if (q.MaxPrice is decimal maxPrice)
        {
            homes = homes.Where(h => h.CurrentPrice <= maxPrice);
        }

        if (q.MinSize is decimal minSize)
        {
            homes = homes.Where(h => h.SizeM2 >= minSize);
        }

        if (q.MaxSize is decimal maxSize)
        {
            homes = homes.Where(h => h.SizeM2 <= maxSize);
        }

        if (q.MaxPricePerM2 is decimal maxPpm)
        {
            homes = homes.Where(h => h.PricePerM2 <= maxPpm);
        }

        if (!string.IsNullOrWhiteSpace(q.PropertyType))
        {
            var type = q.PropertyType.Trim().ToLower();
            homes = homes.Where(h => h.PropertyType != null && h.PropertyType.ToLower() == type);
        }

        if (q.MinRooms is decimal minRooms)
        {
            homes = homes.Where(h => h.RoomCount >= minRooms);
        }

        if (q.Sort == HomeSort.PriceDrop)
        {
            homes = homes.Where(h => h.PreviousPrice != null && h.CurrentPrice < h.PreviousPrice);
        }

        return homes;
    }

    internal static IQueryable<Home> Sort(IQueryable<Home> homes, HomeSort sort) =>
        sort switch
        {
            HomeSort.PriceAsc => homes
                .OrderBy(h => h.CurrentPrice == null)
                .ThenBy(h => h.CurrentPrice)
                .ThenBy(h => h.Id),
            HomeSort.PriceDesc => homes
                .OrderBy(h => h.CurrentPrice == null)
                .ThenByDescending(h => h.CurrentPrice)
                .ThenBy(h => h.Id),
            HomeSort.PriceDrop => homes.OrderByDescending(h => h.PriceChangedAt).ThenBy(h => h.Id),
            HomeSort.PricePerM2 => homes
                .OrderBy(h => h.PricePerM2 == null)
                .ThenBy(h => h.PricePerM2)
                .ThenBy(h => h.Id),
            HomeSort.Size => homes
                .OrderBy(h => h.SizeM2 == null)
                .ThenByDescending(h => h.SizeM2)
                .ThenBy(h => h.Id),
            HomeSort.RecentlySeen => homes.OrderByDescending(h => h.LastSeenAt).ThenBy(h => h.Id),
            _ => homes.OrderByDescending(h => h.FirstSeenAt).ThenBy(h => h.Id),
        };
}

/// <summary>Everything the Home page shows.</summary>
public sealed class GetHomeHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task<HomeDetailResponse> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var summary =
            await HomeProjection
                .Summaries(db, db.Homes.AsNoTracking().Where(h => h.Id == id))
                .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Home", id);

        var listings = await db
            .Listings.AsNoTracking()
            .Include(l => l.Prices)
            .Where(l => l.HomeId == id)
            .OrderBy(l => l.FirstSeenAt)
            .ToListAsync(cancellationToken);
        var listingIds = listings.Select(l => l.Id).ToList();
        var watchRefs = await db
            .WatchSightings.AsNoTracking()
            .Where(s => listingIds.Contains(s.ListingId))
            .Join(
                db.Watches,
                s => s.WatchId,
                w => w.Id,
                (s, w) =>
                    new
                    {
                        s.ListingId,
                        w.Id,
                        w.Name,
                    }
            )
            .ToListAsync(cancellationToken);
        var watchesByListing = watchRefs.ToLookup(x => x.ListingId);

        var primary = listings.FirstOrDefault(l => l.Id == summary.PrimaryListingId);
        var timeline = listings
            .Select(l => new HomeListingResponse(
                l.Id,
                l.ProviderId,
                l.ExternalId,
                l.Url,
                l.Title,
                l.ImageUrl,
                l.CurrentPrice,
                l.Currency,
                l.FirstSeenAt,
                l.LastSeenAt,
                l.DelistedAt,
                HomeListingClassifier.RoleOf(l, listings),
                watchesByListing[l.Id]
                    .Select(w => new WatchRefResponse(w.Id, w.Name))
                    .DistinctBy(w => w.Id)
                    .ToList()
            ))
            .ToList();
        var history = listings
            .SelectMany(l =>
                l.Prices.Select(p => new PricePointResponse(
                    l.Id,
                    p.ObservedAt,
                    p.Amount,
                    p.Currency
                ))
            )
            .OrderBy(p => p.ObservedAt)
            .ToList();
        var end = summary.OffMarketAt ?? clock.GetUtcNow();
        var days = (int)Math.Max(0, Math.Floor((end - summary.FirstSeenAt).TotalDays));
        var matches = await HomeMatchReader.ReadAsync(
            db,
            db.HomeMatches.Where(m =>
                m.State == HomeMatchState.Possible
                && (m.HomeId == id || listingIds.Contains(m.ListingId))
            ),
            cancellationToken
        );

        return new HomeDetailResponse(
            summary,
            primary?.Description,
            primary?.YearText,
            timeline,
            history,
            days,
            matches
        );
    }
}

/// <summary>The Bookmark board.</summary>
public sealed class GetBookmarkBoardHandler(IAppDbContext db)
{
    public async Task<BookmarkBoardResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var homes = await HomeProjection
            .Summaries(
                db,
                db.Homes.AsNoTracking()
                    .Where(h => h.Bookmark != null && h.DismissedAt == null)
                    .OrderByDescending(h => h.Bookmark!.UpdatedAt)
            )
            .ToListAsync(cancellationToken);
        var columns = Enum.GetValues<BookmarkStage>()
            .Select(stage => new BookmarkColumnResponse(
                stage,
                homes.Where(h => h.Bookmark!.Stage == stage).ToList()
            ))
            .ToList();
        return new BookmarkBoardResponse(columns);
    }
}

/// <summary>Loads HomeMatches with both sides for review.</summary>
public static class HomeMatchReader
{
    public static async Task<IReadOnlyList<HomeMatchResponse>> ReadAsync(
        IAppDbContext db,
        IQueryable<HomeMatch> matches,
        CancellationToken cancellationToken
    )
    {
        var list = await matches
            .AsNoTracking()
            .OrderByDescending(m => m.Score)
            .Take(100)
            .ToListAsync(cancellationToken);
        if (list.Count == 0)
        {
            return [];
        }

        var homeIds = list.Select(m => m.HomeId).Distinct().ToList();
        var listingIds = list.Select(m => m.ListingId).Distinct().ToList();
        var homes = await HomeProjection
            .Summaries(db, db.Homes.AsNoTracking().Where(h => homeIds.Contains(h.Id)))
            .ToDictionaryAsync(h => h.Id, cancellationToken);
        var primaryIds = homes.Values.Select(h => h.PrimaryListingId).OfType<Guid>().ToList();
        var listings = await db
            .Listings.AsNoTracking()
            .Where(l => listingIds.Contains(l.Id) || primaryIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        return list.Where(m => homes.ContainsKey(m.HomeId) && listings.ContainsKey(m.ListingId))
            .Select(m =>
            {
                var home = homes[m.HomeId];
                var primary =
                    home.PrimaryListingId is Guid pid && listings.TryGetValue(pid, out var p)
                        ? HomeProjection.ToMatchListing(p)
                        : null;
                return new HomeMatchResponse(
                    m.Id,
                    m.State,
                    m.Score,
                    HomeProjection.ToResponse(m.Signals),
                    m.CreatedAt,
                    m.ReviewedAt,
                    HomeProjection.ToMatchListing(listings[m.ListingId]),
                    home,
                    primary
                );
            })
            .ToList();
    }
}

/// <summary>Lists HomeMatches, Possible ones by default.</summary>
public sealed class ListMatchesHandler(IAppDbContext db)
{
    public Task<IReadOnlyList<HomeMatchResponse>> HandleAsync(
        HomeMatchState? state,
        CancellationToken cancellationToken
    )
    {
        var s = state ?? HomeMatchState.Possible;
        return HomeMatchReader.ReadAsync(
            db,
            db.HomeMatches.Where(m => m.State == s),
            cancellationToken
        );
    }
}
