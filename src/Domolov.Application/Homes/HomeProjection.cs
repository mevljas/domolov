using Domolov.Application.Abstractions;
using Domolov.Domain.Homes;
using Domolov.Domain.Listings;

namespace Domolov.Application.Homes;

/// <summary>SQL projections shared by Home queries.</summary>
public static class HomeProjection
{
    public static IQueryable<HomeSummaryResponse> Summaries(
        IAppDbContext db,
        IQueryable<Home> homes
    ) =>
        homes
            .Select(h => new
            {
                Home = h,
                Primary = db
                    .Listings.Where(l => l.Id == h.PrimaryListingId)
                    .Select(l => new
                    {
                        l.Rooms,
                        l.SizeText,
                        l.LandSizeText,
                        l.FloorText,
                        l.YearBuilt,
                        l.Url,
                        l.ProviderId,
                    })
                    .FirstOrDefault(),
            })
            .Select(x => new HomeSummaryResponse(
                x.Home.Id,
                x.Home.Title,
                x.Home.ImageUrl,
                x.Home.Location,
                x.Home.PropertyType,
                x.Home.RoomCount,
                x.Primary == null ? null : x.Primary.Rooms,
                x.Home.SizeM2,
                x.Primary == null ? null : x.Primary.SizeText,
                x.Home.LandSizeM2,
                x.Primary == null ? null : x.Primary.LandSizeText,
                x.Primary == null ? null : x.Primary.FloorText,
                x.Primary == null ? null : x.Primary.YearBuilt,
                x.Home.CurrentPrice,
                x.Home.PreviousPrice,
                x.Home.Currency,
                x.Home.PriceChangedAt,
                x.Home.PricePerM2,
                x.Home.FirstSeenAt,
                x.Home.LastSeenAt,
                x.Home.OffMarketAt,
                x.Home.SeenAt == null,
                x.Home.DismissedAt != null,
                x.Home.ListingCount,
                x.Home.ActiveListingCount,
                x.Home.RepostCount,
                x.Home.PrimaryListingId,
                x.Primary == null ? null : x.Primary.Url,
                x.Primary == null ? null : x.Primary.ProviderId,
                x.Home.Bookmark == null
                    ? null
                    : new BookmarkResponse(
                        x.Home.Bookmark.Stage,
                        x.Home.Bookmark.Note,
                        x.Home.Bookmark.CreatedAt,
                        x.Home.Bookmark.UpdatedAt
                    )
            ));

    public static MatchListingResponse ToMatchListing(Listing l) =>
        new(
            l.Id,
            l.HomeId,
            l.Title,
            l.Url,
            l.ImageUrl,
            l.CurrentPrice,
            l.Currency,
            l.Location,
            l.PropertyType,
            l.Rooms,
            l.SizeText,
            l.LandSizeText,
            l.FloorText,
            l.YearText,
            l.Description,
            l.FirstSeenAt,
            l.DelistedAt
        );

    public static MatchSignalsResponse ToResponse(MatchSignals s) =>
        new(
            s.PhotoDistance,
            s.PhotoScore,
            s.TitleSimilarity,
            s.DescriptionSimilarity,
            s.TextScore,
            s.AttributeScore,
            s.MatchedAttributes,
            s.MismatchedAttributes
        );
}
