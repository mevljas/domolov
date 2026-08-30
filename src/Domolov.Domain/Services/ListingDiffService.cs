using Domolov.Domain.Enums;
using Domolov.Domain.Providers;

namespace Domolov.Domain.Services;

/// <summary>Kind of change detected for a listing during a Scan.</summary>
public enum ListingChangeKind
{
    Unchanged = 0,
    New = 1,
    PriceDecreased = 2,
    PriceIncreased = 3,
}

/// <summary>Result of comparing a crawled card to stored state.</summary>
public sealed record ListingDiff(ListingCard Card, ListingChangeKind Kind, decimal? PreviousPrice);

/// <summary>Pure listing comparison and notification trigger matching.</summary>
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
            _ => false,
        };
    }
}
