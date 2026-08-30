using Domolov.Domain.Enums;

namespace Domolov.Domain.Entities;

/// <summary>Saved crawl target.</summary>
public sealed class Watch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string ProviderId { get; set; }
    public required string SearchUrl { get; set; }
    public string Cron { get; set; } = "0 * * * *";
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastScannedAt { get; set; }
    public bool HasCompletedBaseline { get; set; }

    public ICollection<NotificationRoute> NotificationRoutes { get; set; } =
        new List<NotificationRoute>();
    public ICollection<ScanRun> ScanRuns { get; set; } = new List<ScanRun>();
    public ICollection<WatchSighting> Sightings { get; set; } = new List<WatchSighting>();
}

/// <summary>One execution of a Watch.</summary>
public sealed class ScanRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WatchId { get; set; }
    public Watch? Watch { get; set; }
    public ScanRunStatus Status { get; set; } = ScanRunStatus.Queued;
    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int PagesScanned { get; set; }
    public int NewCount { get; set; }
    public int PriceChangeCount { get; set; }
    public string? ErrorSummary { get; set; }
    public string? NotifyErrorSummary { get; set; }
}

/// <summary>Provider-scoped property advertisement.</summary>
public sealed class Listing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ProviderId { get; set; }
    public required string ExternalId { get; set; }
    public required string Url { get; set; }
    public required string Title { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public string? PropertyType { get; set; }
    public string? Rooms { get; set; }
    public string? SizeText { get; set; }
    public string? YearText { get; set; }
    public string? FloorText { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PriceObservation> Prices { get; set; } = new List<PriceObservation>();
    public ICollection<WatchSighting> Sightings { get; set; } = new List<WatchSighting>();
    public Bookmark? Bookmark { get; set; }
}

/// <summary>Point-in-time price for a Listing.</summary>
public sealed class PriceObservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTimeOffset ObservedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Listing observed under a Watch.</summary>
public sealed class WatchSighting
{
    public Guid WatchId { get; set; }
    public Watch? Watch { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Operator favorite pointing at a Listing.</summary>
public sealed class Bookmark
{
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Per-Watch delivery rule.</summary>
public sealed class NotificationRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WatchId { get; set; }
    public Watch? Watch { get; set; }
    public NotificationChannel Channel { get; set; }
    public required string Destination { get; set; }
    public NotificationTrigger Triggers { get; set; } =
        NotificationTrigger.NewListing | NotificationTrigger.PriceDecreased;
    public bool IsEnabled { get; set; } = true;
}

/// <summary>Browser Web Push subscription.</summary>
public sealed class PushSubscriptionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Endpoint { get; set; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
