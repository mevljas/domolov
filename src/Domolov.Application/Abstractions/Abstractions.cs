using Domolov.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Application.Abstractions;

/// <summary>EF Core application database boundary.</summary>
public interface IAppDbContext
{
    DbSet<Watch> Watches { get; }
    DbSet<ScanRun> ScanRuns { get; }
    DbSet<Listing> Listings { get; }
    DbSet<PriceObservation> PriceObservations { get; }
    DbSet<WatchSighting> WatchSightings { get; }
    DbSet<Bookmark> Bookmarks { get; }
    DbSet<NotificationRoute> NotificationRoutes { get; }
    DbSet<PushSubscriptionEntity> PushSubscriptions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Sends a notification payload on a specific channel.</summary>
public interface INotifier
{
    Domain.Enums.NotificationChannel Channel { get; }
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Outbound notification content.</summary>
public sealed record NotificationMessage(
    Domain.Enums.NotificationChannel Channel,
    string Destination,
    string Title,
    string Body,
    string? Url,
    string? ImageUrl,
    decimal? Price,
    IReadOnlyList<decimal>? PreviousPrices,
    string? Currency = null,
    string? Location = null,
    string? PropertyType = null,
    string? Rooms = null,
    string? SizeText = null,
    string? YearText = null,
    string? FloorText = null,
    string? LandSizeText = null,
    string? Description = null
);

/// <summary>Options bound from environment / configuration.</summary>
public sealed class DomolovOptions
{
    public const string SectionName = "Domolov";

    public string AdminPassword { get; set; } = "";
    public string TimeZone { get; set; } = "Europe/Ljubljana";
    public int MaxConcurrentScans { get; set; } = 1;
    public int ScanCooldownMs { get; set; } = 20_000;
    public int CloudflareChallengeWaitMs { get; set; } = 30_000;
    public bool BrowserHeadless { get; set; }
    public string BrowserUserDataDir { get; set; } = "browser-profile";
    public string Role { get; set; } = "all";
    public string? TelegramBotToken { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFrom { get; set; }
    public string? VapidPublicKey { get; set; }
    public string? VapidPrivateKey { get; set; }
    public string? VapidSubject { get; set; }
}

/// <summary>Queues and executes Watch scans.</summary>
public interface IScanOrchestrator
{
    /// <summary>Queues a ScanRun for the Watch.</summary>
    /// <param name="watchId">Watch to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="force">
    /// When true, clears Cloudflare backoff on the Watch and enqueues anyway (Run now).
    /// </param>
    Task<Guid> EnqueueAsync(
        Guid watchId,
        CancellationToken cancellationToken = default,
        bool force = false
    );
    Task ProcessQueuedAsync(CancellationToken cancellationToken = default);
}

/// <summary>Resolves IListingProvider by URL or id.</summary>
public interface IListingProviderResolver
{
    Domain.Providers.IListingProvider Resolve(Uri searchUrl);
    Domain.Providers.IListingProvider? TryGetById(string providerId);
}
