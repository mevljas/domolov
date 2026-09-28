using Domolov.Domain.Common;

namespace Domolov.Domain.Listings;

/// <summary>A point-in-time price recorded for a Listing.</summary>
public sealed class PriceObservation
{
    private PriceObservation()
    {
        Currency = "EUR";
    }

    public PriceObservation(Guid listingId, decimal amount, string currency, DateTimeOffset now)
    {
        Id = Ids.New();
        ListingId = listingId;
        Amount = amount;
        Currency = currency;
        ObservedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public DateTimeOffset ObservedAt { get; private set; }
}
