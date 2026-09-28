using Domolov.Domain.Listings;

namespace Domolov.Domain.Homes;

/// <summary>Role of a Listing within its Home's history.</summary>
public enum HomeListingRole
{
    /// <summary>The first Listing seen for the Home.</summary>
    Original = 0,

    /// <summary>Appeared after every earlier Listing of the Home was Delisted.</summary>
    Repost = 1,

    /// <summary>Appeared while another Listing of the Home was still active.</summary>
    Duplicate = 2,
}

/// <summary>Orders a Home's Listings into Original, Reposts and Duplicates and picks the primary.</summary>
public static class HomeListingClassifier
{
    /// <summary>Most recently first seen active Listing; if none are active, the last one seen.</summary>
    public static Listing SelectPrimary(IReadOnlyCollection<Listing> listings)
    {
        var active = listings.Where(l => !l.IsDelisted).ToList();
        return active.Count > 0
            ? active.OrderByDescending(l => l.FirstSeenAt).ThenBy(l => l.Id).First()
            : listings.OrderByDescending(l => l.LastSeenAt).ThenBy(l => l.Id).First();
    }

    public static HomeListingRole RoleOf(Listing listing, IReadOnlyCollection<Listing> homeListings)
    {
        var earlier = homeListings
            .Where(l => l.Id != listing.Id && l.FirstSeenAt < listing.FirstSeenAt)
            .ToList();
        if (earlier.Count == 0)
        {
            return HomeListingRole.Original;
        }

        return earlier.All(l => l.DelistedAt is { } d && d <= listing.FirstSeenAt)
            ? HomeListingRole.Repost
            : HomeListingRole.Duplicate;
    }

    public static int CountReposts(IReadOnlyCollection<Listing> listings) =>
        listings.Count(l => RoleOf(l, listings) == HomeListingRole.Repost);
}
