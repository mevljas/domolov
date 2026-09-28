using System.ComponentModel.DataAnnotations;
using Domolov.Domain.Homes;

namespace Domolov.Application.Homes;

/// <summary>A Bookmark as returned by the API.</summary>
public sealed record BookmarkResponse(
    BookmarkStage Stage,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>A Home card: the Home plus key facts of its Primary Listing.</summary>
public sealed record HomeSummaryResponse(
    Guid Id,
    string Title,
    string? ImageUrl,
    string? Location,
    string? PropertyType,
    decimal? RoomCount,
    string? Rooms,
    decimal? SizeM2,
    string? SizeText,
    decimal? LandSizeM2,
    string? LandSizeText,
    string? FloorText,
    int? YearBuilt,
    decimal? Price,
    decimal? PreviousPrice,
    string Currency,
    DateTimeOffset? PriceChangedAt,
    decimal? PricePerM2,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? OffMarketAt,
    bool IsUnseen,
    bool IsDismissed,
    int ListingCount,
    int ActiveListingCount,
    int RepostCount,
    Guid? PrimaryListingId,
    string? Url,
    string? ProviderId,
    BookmarkResponse? Bookmark
);

/// <summary>A price point of one of the Home's Listings.</summary>
public sealed record PricePointResponse(
    Guid ListingId,
    DateTimeOffset ObservedAt,
    decimal Amount,
    string Currency
);

/// <summary>A Watch that sighted a Listing.</summary>
public sealed record WatchRefResponse(Guid Id, string Name);

/// <summary>One ad in the Home's history.</summary>
public sealed record HomeListingResponse(
    Guid Id,
    string ProviderId,
    string ExternalId,
    string Url,
    string Title,
    string? ImageUrl,
    decimal? Price,
    string Currency,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? DelistedAt,
    HomeListingRole Role,
    IReadOnlyList<WatchRefResponse> Watches
);

/// <summary>Everything the Home page shows.</summary>
public sealed record HomeDetailResponse(
    HomeSummaryResponse Home,
    string? Description,
    string? YearText,
    IReadOnlyList<HomeListingResponse> Listings,
    IReadOnlyList<PricePointResponse> PriceHistory,
    int TimeOnMarketDays,
    IReadOnlyList<HomeMatchResponse> PossibleMatches
);

/// <summary>Sort orders for the Homes feed.</summary>
public enum HomeSort
{
    Newest = 0,
    PriceAsc = 1,
    PriceDesc = 2,
    PriceDrop = 3,
    PricePerM2 = 4,
    Size = 5,
    RecentlySeen = 6,
}

/// <summary>Market filter for the Homes feed.</summary>
public enum MarketStatus
{
    OnMarket = 0,
    OffMarket = 1,
    All = 2,
}

/// <summary>Whether Dismissed Homes are excluded, included, or the only ones shown.</summary>
public enum DismissedFilter
{
    Exclude = 0,
    Include = 1,
    Only = 2,
}

/// <summary>Filters, sort and paging for the Homes feed.</summary>
public sealed record HomeSearchQuery(
    Guid? WatchId = null,
    string? Q = null,
    MarketStatus? Status = null,
    bool? Bookmarked = null,
    BookmarkStage? Stage = null,
    bool? Unseen = null,
    DismissedFilter? Dismissed = null,
    bool? Reposted = null,
    bool? HasDuplicates = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    decimal? MinSize = null,
    decimal? MaxSize = null,
    decimal? MaxPricePerM2 = null,
    string? PropertyType = null,
    decimal? MinRooms = null,
    HomeSort? Sort = null,
    int? Page = null,
    int? PageSize = null
);

/// <summary>Create or update a Bookmark.</summary>
public sealed record SetBookmarkRequest(
    BookmarkStage Stage,
    [property: MaxLength(4000)] string? Note
);

/// <summary>Manually link an ad to a Home.</summary>
public sealed record LinkListingRequest(Guid ListingId);

/// <summary>Mark Homes seen up to a point in time (defaults to now).</summary>
public sealed record MarkAllSeenRequest(DateTimeOffset? Before = null);

/// <summary>Number of Homes affected by a bulk operation.</summary>
public sealed record CountResponse(int Count);

/// <summary>The Bookmark board: Homes grouped by stage.</summary>
public sealed record BookmarkBoardResponse(IReadOnlyList<BookmarkColumnResponse> Columns);

/// <summary>One board column.</summary>
public sealed record BookmarkColumnResponse(
    BookmarkStage Stage,
    IReadOnlyList<HomeSummaryResponse> Homes
);

/// <summary>Why two ads look like the same Home.</summary>
public sealed record MatchSignalsResponse(
    int? PhotoDistance,
    double? PhotoScore,
    double? TitleSimilarity,
    double? DescriptionSimilarity,
    double? TextScore,
    double? AttributeScore,
    IReadOnlyList<string> MatchedAttributes,
    IReadOnlyList<string> MismatchedAttributes
);

/// <summary>The ad side of a HomeMatch.</summary>
public sealed record MatchListingResponse(
    Guid Id,
    Guid HomeId,
    string Title,
    string Url,
    string? ImageUrl,
    decimal? Price,
    string Currency,
    string? Location,
    string? PropertyType,
    string? Rooms,
    string? SizeText,
    string? LandSizeText,
    string? FloorText,
    string? YearText,
    string? Description,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset? DelistedAt
);

/// <summary>A HomeMatch with both sides for side-by-side review.</summary>
public sealed record HomeMatchResponse(
    Guid Id,
    HomeMatchState State,
    double Score,
    MatchSignalsResponse Signals,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    MatchListingResponse Listing,
    HomeSummaryResponse Home,
    MatchListingResponse? HomePrimaryListing
);

/// <summary>Confirm or reject a Possible match.</summary>
public sealed record ReviewMatchRequest(HomeMatchState State);
