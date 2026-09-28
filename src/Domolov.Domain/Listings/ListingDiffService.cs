using Domolov.Domain.Notifications;

namespace Domolov.Domain.Listings;

/// <summary>Kind of change detected for a Listing or Home during a ScanRun.</summary>
public enum ListingChangeKind
{
    Unchanged = 0,
    New = 1,
    PriceDecreased = 2,
    PriceIncreased = 3,

    /// <summary>A new Listing of a Home whose earlier Listing was Delisted.</summary>
    Reposted = 4,
}

/// <summary>Pure price comparison and notification trigger matching.</summary>
public static class ListingDiffService
{
    public static ListingChangeKind Classify(decimal? previousPrice, decimal? currentPrice)
    {
        if (previousPrice is null)
        {
            return ListingChangeKind.New;
        }

        if (currentPrice is null || previousPrice == currentPrice)
        {
            return ListingChangeKind.Unchanged;
        }

        return currentPrice < previousPrice
            ? ListingChangeKind.PriceDecreased
            : ListingChangeKind.PriceIncreased;
    }

    public static bool MatchesTrigger(NotificationTrigger triggers, ListingChangeKind kind)
    {
        if (triggers == NotificationTrigger.None || kind == ListingChangeKind.Unchanged)
        {
            return false;
        }

        return kind switch
        {
            ListingChangeKind.New => triggers.HasFlag(NotificationTrigger.NewListing),
            ListingChangeKind.PriceDecreased => triggers.HasFlag(NotificationTrigger.PriceDecreased)
                || triggers.HasFlag(NotificationTrigger.AnyPriceChange),
            ListingChangeKind.PriceIncreased => triggers.HasFlag(NotificationTrigger.PriceIncreased)
                || triggers.HasFlag(NotificationTrigger.AnyPriceChange),
            ListingChangeKind.Reposted => triggers.HasFlag(NotificationTrigger.Reposted),
            _ => false,
        };
    }
}
