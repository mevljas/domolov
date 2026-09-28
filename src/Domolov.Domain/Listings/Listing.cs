using Domolov.Domain.Common;
using Domolov.Domain.Homes;
using Domolov.Domain.Providers;

namespace Domolov.Domain.Listings;

/// <summary>One provider advertisement for a Home, identified by provider and external id.</summary>
public sealed class Listing
{
    public const int TitleMaxLength = 500;

    private readonly List<PriceObservation> _prices = [];

    private Listing()
    {
        ProviderId = "";
        ExternalId = "";
        Url = "";
        Title = "";
        Currency = "EUR";
    }

    public Guid Id { get; private set; }
    public Guid HomeId { get; private set; }
    public string ProviderId { get; private set; }
    public string ExternalId { get; private set; }
    public string Url { get; private set; }
    public string Title { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? Description { get; private set; }
    public string? PropertyType { get; private set; }
    public string? Rooms { get; private set; }
    public string? SizeText { get; private set; }
    public string? YearText { get; private set; }
    public string? FloorText { get; private set; }
    public string? Location { get; private set; }
    public string? LandSizeText { get; private set; }

    public decimal? SizeM2 { get; private set; }
    public decimal? LandSizeM2 { get; private set; }
    public decimal? RoomCount { get; private set; }
    public int? YearBuilt { get; private set; }

    public decimal? CurrentPrice { get; private set; }
    public decimal? PreviousPrice { get; private set; }
    public string Currency { get; private set; }
    public DateTimeOffset? PriceChangedAt { get; private set; }
    public decimal? PricePerM2 { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Set when the Listing is Delisted; cleared when it is Relisted.</summary>
    public DateTimeOffset? DelistedAt { get; private set; }

    /// <summary>64-bit perceptual hash (dHash) of the main photo, when captured.</summary>
    public long? ImageHash { get; private set; }
    public string? NormalizedTitle { get; private set; }
    public string? NormalizedDescription { get; private set; }

    public IReadOnlyCollection<PriceObservation> Prices => _prices;

    public bool IsDelisted => DelistedAt is not null;

    public static Listing Create(
        Guid homeId,
        string providerId,
        ListingCard card,
        DateTimeOffset now
    )
    {
        var listing = new Listing
        {
            Id = Ids.New(),
            HomeId = homeId,
            ProviderId = providerId,
            ExternalId = card.ExternalId,
            FirstSeenAt = now,
            LastSeenAt = now,
        };
        listing.Apply(card, overwriteWithNulls: true);
        listing.RecordPrice(card.Price, card.Currency, now);
        return listing;
    }

    /// <summary>Updates scraped fields from a fresh card and relists a Delisted Listing.</summary>
    public void Refresh(ListingCard card, DateTimeOffset now)
    {
        Apply(card, overwriteWithNulls: false);
        LastSeenAt = now;
        DelistedAt = null;
    }

    /// <summary>Records the card price; returns whether it moved relative to the last price.</summary>
    public ListingChangeKind RecordPrice(decimal? amount, string? currency, DateTimeOffset now)
    {
        if (amount is not decimal price)
        {
            return ListingChangeKind.Unchanged;
        }

        var cur = string.IsNullOrWhiteSpace(currency) ? "EUR" : currency.Trim();
        if (CurrentPrice is not decimal previous)
        {
            AddObservation(price, cur, now);
            CurrentPrice = price;
            Currency = cur;
            UpdatePricePerM2();
            return ListingChangeKind.Unchanged;
        }

        if (previous == price)
        {
            return ListingChangeKind.Unchanged;
        }

        AddObservation(price, cur, now);
        PreviousPrice = previous;
        CurrentPrice = price;
        Currency = cur;
        PriceChangedAt = now;
        UpdatePricePerM2();
        return price < previous
            ? ListingChangeKind.PriceDecreased
            : ListingChangeKind.PriceIncreased;
    }

    public void Delist(DateTimeOffset now) => DelistedAt ??= now;

    /// <summary>Recomputes parsed attributes, normalised text and price per m2 from stored text.</summary>
    public void RecomputeDerived()
    {
        var parsed = ListingAttributeParser.Parse(SizeText, LandSizeText, Rooms, YearText);
        SizeM2 = parsed.SizeM2;
        LandSizeM2 = parsed.LandSizeM2;
        RoomCount = parsed.RoomCount;
        YearBuilt = parsed.YearBuilt;
        NormalizedTitle = TextNormalizer.Normalize(Title) ?? "";
        NormalizedDescription = TextNormalizer.Normalize(Description);
        UpdatePricePerM2();
    }

    public void AssignHome(Guid homeId) => HomeId = homeId;

    public MatchCandidate ToMatchCandidate() =>
        new(
            PropertyType,
            RoomCount,
            SizeM2,
            LandSizeM2,
            Location,
            YearBuilt,
            FloorText,
            ImageHash,
            NormalizedTitle,
            NormalizedDescription
        );

    private void AddObservation(decimal amount, string currency, DateTimeOffset now) =>
        _prices.Add(new PriceObservation(Id, amount, currency, now));

    private void Apply(ListingCard card, bool overwriteWithNulls)
    {
        Url = card.Url;
        Title = Truncate(string.IsNullOrWhiteSpace(card.Title) ? card.ExternalId : card.Title);
        ImageUrl = Pick(card.ImageUrl, ImageUrl, overwriteWithNulls);
        Description = Pick(card.Description, Description, overwriteWithNulls);
        PropertyType = Pick(card.PropertyType, PropertyType, overwriteWithNulls);
        Rooms = Pick(card.Rooms, Rooms, overwriteWithNulls);
        SizeText = Pick(card.SizeText, SizeText, overwriteWithNulls);
        YearText = Pick(card.YearText, YearText, overwriteWithNulls);
        FloorText = Pick(card.FloorText, FloorText, overwriteWithNulls);
        Location = Pick(card.Location, Location, overwriteWithNulls);
        LandSizeText = Pick(card.LandSizeText, LandSizeText, overwriteWithNulls);
        if (card.ImageHash is not null || overwriteWithNulls)
        {
            ImageHash = card.ImageHash ?? ImageHash;
        }

        var parsed = ListingAttributeParser.Parse(SizeText, LandSizeText, Rooms, YearText);
        SizeM2 = parsed.SizeM2;
        LandSizeM2 = parsed.LandSizeM2;
        RoomCount = parsed.RoomCount;
        YearBuilt = parsed.YearBuilt;
        NormalizedTitle = TextNormalizer.Normalize(Title);
        NormalizedDescription = TextNormalizer.Normalize(Description);
        UpdatePricePerM2();
    }

    private void UpdatePricePerM2() =>
        PricePerM2 =
            CurrentPrice is decimal p && SizeM2 is decimal s && s > 0
                ? decimal.Round(p / s, 2, MidpointRounding.AwayFromZero)
                : null;

    private static string? Pick(string? fresh, string? existing, bool overwriteWithNulls) =>
        !string.IsNullOrWhiteSpace(fresh) ? fresh.Trim()
        : overwriteWithNulls ? null
        : existing;

    private static string Truncate(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= TitleMaxLength ? trimmed : trimmed[..TitleMaxLength];
    }
}
