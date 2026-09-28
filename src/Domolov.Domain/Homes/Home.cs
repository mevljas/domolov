using Domolov.Domain.Common;
using Domolov.Domain.Listings;

namespace Domolov.Domain.Homes;

/// <summary>
/// The real-world dwelling behind one or more Listings. Feeds show one card per Home,
/// represented by its Primary Listing; Bookmark, Dismissal and Unseen live here.
/// </summary>
public sealed class Home
{
    private Home()
    {
        Title = "";
        Currency = "EUR";
    }

    public Home(DateTimeOffset now)
        : this()
    {
        Id = Ids.New();
        FirstSeenAt = now;
        LastSeenAt = now;
    }

    public Guid Id { get; private set; }
    public Guid? PrimaryListingId { get; private set; }

    /// <summary>Dismissed Homes are hidden from feeds and never notified.</summary>
    public DateTimeOffset? DismissedAt { get; private set; }

    /// <summary>Null means Unseen.</summary>
    public DateTimeOffset? SeenAt { get; private set; }
    public DateTimeOffset FirstSeenAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Set when every Listing of the Home is Delisted.</summary>
    public DateTimeOffset? OffMarketAt { get; private set; }

    // Denormalised from the Primary Listing so feeds filter and sort in SQL.
    public string Title { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? Location { get; private set; }
    public string? PropertyType { get; private set; }
    public decimal? RoomCount { get; private set; }
    public decimal? SizeM2 { get; private set; }
    public decimal? LandSizeM2 { get; private set; }
    public decimal? CurrentPrice { get; private set; }
    public decimal? PreviousPrice { get; private set; }
    public string Currency { get; private set; }
    public DateTimeOffset? PriceChangedAt { get; private set; }
    public decimal? PricePerM2 { get; private set; }
    public int ListingCount { get; private set; }
    public int ActiveListingCount { get; private set; }
    public int RepostCount { get; private set; }

    public Bookmark? Bookmark { get; private set; }
    public uint Version { get; private set; }

    public bool IsDismissed => DismissedAt is not null;
    public bool IsUnseen => SeenAt is null;
    public bool IsOffMarket => OffMarketAt is not null;

    public void MarkSeen(DateTimeOffset now) => SeenAt ??= now;

    public void MarkUnseen() => SeenAt = null;

    /// <summary>Hides the Home everywhere; its Bookmark is removed.</summary>
    public void Dismiss(DateTimeOffset now)
    {
        DismissedAt ??= now;
        Bookmark = null;
    }

    public void Restore() => DismissedAt = null;

    public Bookmark SetBookmark(BookmarkStage stage, string? note, DateTimeOffset now)
    {
        if (IsDismissed)
        {
            throw new DomainRuleException("home", "Restore the Home before bookmarking it.");
        }

        if (Bookmark is null)
        {
            Bookmark = new Bookmark(Id, stage, note, now);
        }
        else
        {
            Bookmark.Update(stage, note, now);
        }

        return Bookmark;
    }

    public void RemoveBookmark() => Bookmark = null;

    /// <summary>
    /// Recomputes the Primary Listing, denormalised fields, market state and repost count from
    /// all Listings of this Home. A lower price makes the Home Unseen again.
    /// </summary>
    public HomePriceChange Refresh(IReadOnlyCollection<Listing> listings, DateTimeOffset now)
    {
        if (listings.Count == 0)
        {
            return HomePriceChange.None;
        }

        var primary = HomeListingClassifier.SelectPrimary(listings);
        PrimaryListingId = primary.Id;
        Title = primary.Title;
        ImageUrl =
            primary.ImageUrl ?? listings.Select(l => l.ImageUrl).FirstOrDefault(u => u is not null);
        Location = primary.Location;
        PropertyType = primary.PropertyType;
        RoomCount = primary.RoomCount;
        SizeM2 = primary.SizeM2;
        LandSizeM2 = primary.LandSizeM2;
        PricePerM2 = primary.PricePerM2;
        FirstSeenAt = listings.Min(l => l.FirstSeenAt);
        LastSeenAt = listings.Max(l => l.LastSeenAt);
        ListingCount = listings.Count;
        ActiveListingCount = listings.Count(l => !l.IsDelisted);
        OffMarketAt = ActiveListingCount == 0 ? listings.Max(l => l.DelistedAt) : null;
        RepostCount = HomeListingClassifier.CountReposts(listings);

        return ApplyPrice(primary.CurrentPrice, primary.Currency, now);
    }

    /// <summary>
    /// Merges another Home into this one: the more advanced BookmarkStage wins, notes are joined
    /// with a date marker, and the result stays Dismissed if either was.
    /// </summary>
    public void Absorb(Home other, DateTimeOffset now)
    {
        if (other.Id == Id)
        {
            return;
        }

        if (other.DismissedAt is { } otherDismissed)
        {
            DismissedAt = DismissedAt is { } mine && mine < otherDismissed ? mine : otherDismissed;
        }

        if (IsDismissed)
        {
            Bookmark = null;
        }
        else if (other.Bookmark is { } theirs)
        {
            if (Bookmark is null)
            {
                Bookmark = new Bookmark(Id, theirs.Stage, theirs.Note, now);
            }
            else
            {
                var stage = (BookmarkStage)Math.Max((int)Bookmark.Stage, (int)theirs.Stage);
                Bookmark.Update(stage, Bookmark.JoinNote(theirs, now), now);
            }
        }

        if (other.FirstSeenAt < FirstSeenAt)
        {
            FirstSeenAt = other.FirstSeenAt;
        }
    }

    private HomePriceChange ApplyPrice(decimal? price, string currency, DateTimeOffset now)
    {
        Currency = currency;
        if (price is not decimal p)
        {
            return HomePriceChange.None;
        }

        if (CurrentPrice is not decimal current)
        {
            CurrentPrice = p;
            return HomePriceChange.None;
        }

        if (current == p)
        {
            return HomePriceChange.None;
        }

        PreviousPrice = current;
        CurrentPrice = p;
        PriceChangedAt = now;
        if (p < current)
        {
            MarkUnseen();
            return HomePriceChange.Decreased;
        }

        return HomePriceChange.Increased;
    }
}

/// <summary>How the Home's price moved on the last refresh.</summary>
public enum HomePriceChange
{
    None = 0,
    Decreased = 1,
    Increased = 2,
}
