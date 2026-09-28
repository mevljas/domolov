using System.ComponentModel.DataAnnotations;
using Domolov.Application.Notifications;
using Domolov.Application.Scans;

namespace Domolov.Application.Watches;

/// <summary>A Watch as returned by the API.</summary>
public sealed record WatchResponse(
    Guid Id,
    string Name,
    string ProviderId,
    string SearchUrl,
    string Cron,
    bool IsPaused,
    bool HasCompletedBaseline,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastScannedAt,
    DateTimeOffset? NextRunAt,
    int CloudflareStrikeCount,
    DateTimeOffset? CloudflareBlockedUntil,
    int ListingCount,
    ScanRunResponse? LastScan,
    IReadOnlyList<NotificationRouteResponse> Routes,
    uint Version
);

/// <summary>Payload for creating a Watch, optionally with its first notification route.</summary>
public sealed record CreateWatchRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: Required, MaxLength(2000)] string SearchUrl,
    [property: MaxLength(100)] string? Cron = null,
    bool IsPaused = false,
    CreateNotificationRouteRequest? InitialRoute = null
);

/// <summary>Partial update of a Watch; omitted fields stay unchanged.</summary>
public sealed record UpdateWatchRequest(
    [property: MaxLength(200)] string? Name = null,
    [property: MaxLength(2000)] string? SearchUrl = null,
    [property: MaxLength(100)] string? Cron = null,
    bool? IsPaused = null
);

/// <summary>Asks whether a pasted search URL is supported.</summary>
public sealed record SearchUrlCheckRequest([property: Required, MaxLength(2000)] string Url);

/// <summary>Result of a search URL check, with a suggested Watch name.</summary>
public sealed record SearchUrlCheckResponse(
    bool Supported,
    string? ProviderId,
    string? SuggestedName,
    string? Problem
);
