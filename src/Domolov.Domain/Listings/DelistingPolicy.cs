using Domolov.Domain.Watches;

namespace Domolov.Domain.Listings;

/// <summary>Decides when a Listing counts as Delisted.</summary>
public static class DelistingPolicy
{
    /// <summary>Consecutive Complete ScanRuns that must miss the Listing, per Watch.</summary>
    public const int MissedRunThreshold = 2;

    /// <summary>
    /// Delisted when every Watch that sighted the Listing under its current SearchUrl has missed it
    /// in at least <see cref="MissedRunThreshold"/> consecutive Complete ScanRuns. Stale sightings
    /// (made before a SearchUrl change) are ignored; with none left the Listing is not delisted.
    /// </summary>
    public static bool ShouldDelist(IEnumerable<WatchSighting> sightings)
    {
        var current = sightings.Where(s => !s.IsStale).ToList();
        return current.Count > 0 && current.All(s => s.MissedRunCount >= MissedRunThreshold);
    }
}
