using System.ComponentModel.DataAnnotations;
using Domolov.Domain.Enums;

namespace Domolov.Application.Contracts;

/// <summary>Payload for creating a Watch.</summary>
public sealed class CreateWatchRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [Required, Url, MaxLength(2000)]
    public string SearchUrl { get; set; } = "";

    [MaxLength(100)]
    public string Cron { get; set; } = "0 */6 * * *";

    public bool IsEnabled { get; set; } = true;
}

/// <summary>Payload for updating a Watch.</summary>
public sealed class UpdateWatchRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [Required, Url, MaxLength(2000)]
    public string SearchUrl { get; set; } = "";

    [MaxLength(100)]
    public string Cron { get; set; } = "0 */6 * * *";

    public bool IsEnabled { get; set; } = true;
}

/// <summary>Watch returned by the API.</summary>
public sealed record WatchResponse(
    Guid Id,
    string Name,
    string ProviderId,
    string SearchUrl,
    string Cron,
    bool IsEnabled,
    bool HasCompletedBaseline,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastScannedAt,
    int CloudflareStrikeCount,
    DateTimeOffset? CloudflareBlockedUntil,
    IReadOnlyList<NotificationRouteResponse> Routes
);

/// <summary>Notification route DTO.</summary>
public sealed record NotificationRouteResponse(
    Guid Id,
    NotificationChannel Channel,
    string Destination,
    NotificationTrigger Triggers,
    bool IsEnabled
);

/// <summary>Payload for creating a notification route.</summary>
public sealed class CreateNotificationRouteRequest
{
    public NotificationChannel Channel { get; set; }

    [Required, MaxLength(2000)]
    public string Destination { get; set; } = "";

    public NotificationTrigger Triggers { get; set; } =
        NotificationTrigger.NewListing | NotificationTrigger.PriceDecreased;

    public bool IsEnabled { get; set; } = true;
}

/// <summary>Listing list item.</summary>
public sealed record ListingResponse(
    Guid Id,
    string ProviderId,
    string ExternalId,
    string Url,
    string Title,
    string? ImageUrl,
    string? Description,
    string? PropertyType,
    string? Rooms,
    string? SizeText,
    string? YearText,
    string? FloorText,
    string? Location,
    string? LandSizeText,
    decimal? LatestPrice,
    string Currency,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    bool IsBookmarked
);

/// <summary>Result of wiping every listing.</summary>
public sealed record DeleteAllListingsResponse(int Deleted);

/// <summary>Listing detail with price history.</summary>
public sealed record ListingDetailResponse(
    Guid Id,
    string ProviderId,
    string ExternalId,
    string Url,
    string Title,
    string? ImageUrl,
    string? Description,
    string? PropertyType,
    string? Rooms,
    string? SizeText,
    string? YearText,
    string? FloorText,
    string? Location,
    string? LandSizeText,
    bool IsBookmarked,
    IReadOnlyList<PricePointResponse> Prices
);

/// <summary>Price chart point.</summary>
public sealed record PricePointResponse(DateTimeOffset ObservedAt, decimal Amount, string Currency);

/// <summary>ScanRun DTO.</summary>
public sealed record ScanRunResponse(
    Guid Id,
    Guid WatchId,
    string WatchName,
    ScanRunStatus Status,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int PagesScanned,
    int NewCount,
    int PriceChangeCount,
    string? ErrorSummary,
    string? NotifyErrorSummary,
    bool CloudflareBlocked
);

/// <summary>Settings capability flags.</summary>
public sealed record SettingsResponse(
    string TimeZone,
    int MaxConcurrentScans,
    int ScanCooldownMs,
    bool BrowserHeadless,
    bool TelegramConfigured,
    bool SmtpConfigured,
    bool VapidConfigured,
    string? VapidPublicKey,
    string Role
);

/// <summary>Login payload.</summary>
public sealed class LoginRequest
{
    [Required]
    public string Password { get; set; } = "";
}
