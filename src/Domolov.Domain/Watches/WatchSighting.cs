namespace Domolov.Domain.Watches;

/// <summary>A Listing observed under a specific Watch, with first and last seen times.</summary>
public sealed class WatchSighting
{
    private WatchSighting() { }

    public WatchSighting(Guid watchId, Guid listingId, DateTimeOffset now)
    {
        WatchId = watchId;
        ListingId = listingId;
        FirstSeenAt = now;
        LastSeenAt = now;
    }

    public Guid WatchId { get; private set; }
    public Guid ListingId { get; private set; }
    public DateTimeOffset FirstSeenAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Consecutive Complete ScanRuns of the Watch that did not see the Listing.</summary>
    public int MissedRunCount { get; private set; }

    /// <summary>True when the sighting predates the Watch's current SearchUrl.</summary>
    public bool IsStale { get; private set; }

    public void Seen(DateTimeOffset now)
    {
        LastSeenAt = now;
        MissedRunCount = 0;
        IsStale = false;
    }

    public void Missed() => MissedRunCount++;

    public void MarkStale()
    {
        IsStale = true;
        MissedRunCount = 0;
    }
}
