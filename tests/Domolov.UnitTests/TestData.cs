using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using Domolov.Domain.Providers;

namespace Domolov.UnitTests;

/// <summary>Builders for domain objects used across tests.</summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-01T10:00:00Z");

    public static ListingCard Card(
        string id = "100",
        decimal? price = 250_000m,
        string title = "LJUBLJANA, BEŽIGRAD",
        string? description = "Svetlo stanovanje, 72,4 m2, 3/5 nad., zgrajeno l. 2008.",
        string? rooms = "3-sobno",
        string? size = "72.4 m2",
        long? imageHash = null,
        int page = 1
    ) =>
        new(
            id,
            $"https://www.nepremicnine.net/oglasi-prodaja/{id}/",
            title,
            price,
            "EUR",
            $"https://img.nepremicnine.net/{id}.jpg",
            description,
            "Stanovanje",
            rooms,
            size,
            "2008",
            "3/5 nad.",
            Location: title,
            PageIndex: page,
            ImageHash: imageHash
        );

    public static (Home Home, Listing Listing) HomeWith(ListingCard card, DateTimeOffset? at = null)
    {
        var now = at ?? Now;
        var home = new Home(now);
        var listing = Listing.Create(home.Id, "nepremicnine", card, now);
        home.Refresh([listing], now);
        return (home, listing);
    }
}
