using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Application.Homes;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Application.Listings;

/// <summary>One ad with its price history and the Watches that sighted it.</summary>
public sealed record ListingDetailResponse(
    Guid Id,
    Guid HomeId,
    string ProviderId,
    string ExternalId,
    string Url,
    string Title,
    string? ImageUrl,
    string? Description,
    string? PropertyType,
    string? Rooms,
    string? SizeText,
    string? YearText,
    string? FloorText,
    string? Location,
    string? LandSizeText,
    decimal? SizeM2,
    decimal? LandSizeM2,
    decimal? RoomCount,
    int? YearBuilt,
    decimal? Price,
    decimal? PreviousPrice,
    string Currency,
    decimal? PricePerM2,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? DelistedAt,
    IReadOnlyList<PricePointResponse> Prices,
    IReadOnlyList<WatchRefResponse> Watches
);

/// <summary>Number of Listings removed.</summary>
public sealed record DeleteAllListingsResponse(int Deleted);

/// <summary>Gets one ad.</summary>
public sealed class GetListingHandler(IAppDbContext db)
{
    public async Task<ListingDetailResponse> HandleAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var l =
            await db
                .Listings.AsNoTracking()
                .Include(x => x.Prices)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Listing", id);
        var watches = await db
            .WatchSightings.AsNoTracking()
            .Where(s => s.ListingId == id)
            .Join(
                db.Watches,
                s => s.WatchId,
                w => w.Id,
                (s, w) => new WatchRefResponse(w.Id, w.Name)
            )
            .ToListAsync(cancellationToken);
        return new ListingDetailResponse(
            l.Id,
            l.HomeId,
            l.ProviderId,
            l.ExternalId,
            l.Url,
            l.Title,
            l.ImageUrl,
            l.Description,
            l.PropertyType,
            l.Rooms,
            l.SizeText,
            l.YearText,
            l.FloorText,
            l.Location,
            l.LandSizeText,
            l.SizeM2,
            l.LandSizeM2,
            l.RoomCount,
            l.YearBuilt,
            l.CurrentPrice,
            l.PreviousPrice,
            l.Currency,
            l.PricePerM2,
            l.FirstSeenAt,
            l.LastSeenAt,
            l.DelistedAt,
            l.Prices.OrderBy(p => p.ObservedAt)
                .Select(p => new PricePointResponse(l.Id, p.ObservedAt, p.Amount, p.Currency))
                .ToList(),
            watches
        );
    }
}

/// <summary>Deletes every Listing and Home (danger zone).</summary>
public sealed class DeleteAllListingsHandler(IAppDbContext db)
{
    public async Task<DeleteAllListingsResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var listings = await db.Listings.CountAsync(cancellationToken);
        await db.Homes.ExecuteDeleteAsync(cancellationToken);
        return new DeleteAllListingsResponse(listings);
    }
}
