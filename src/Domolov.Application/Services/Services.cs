using Domolov.Application.Abstractions;
using Domolov.Application.Contracts;
using Domolov.Domain.Entities;
using Domolov.Domain.Enums;
using Domolov.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Domolov.Application.Services;

/// <summary>Watch CRUD and mapping.</summary>
public interface IWatchService
{
    Task<IReadOnlyList<WatchResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WatchResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WatchResponse> CreateAsync(
        CreateWatchRequest request,
        CancellationToken cancellationToken = default
    );
    Task<WatchResponse?> UpdateAsync(
        Guid id,
        UpdateWatchRequest request,
        CancellationToken cancellationToken = default
    );
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NotificationRouteResponse?> AddRouteAsync(
        Guid watchId,
        CreateNotificationRouteRequest request,
        CancellationToken cancellationToken = default
    );
    Task<bool> DeleteRouteAsync(
        Guid watchId,
        Guid routeId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Watch CRUD implementation.</summary>
public sealed class WatchService(IAppDbContext db, IListingProviderResolver providers)
    : IWatchService
{
    public async Task<IReadOnlyList<WatchResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var watches = await db
            .Watches.AsNoTracking()
            .Include(w => w.NotificationRoutes)
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken);
        return watches.Select(Map).ToList();
    }

    public async Task<WatchResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var watch = await db
            .Watches.AsNoTracking()
            .Include(w => w.NotificationRoutes)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        return watch is null ? null : Map(watch);
    }

    public async Task<WatchResponse> CreateAsync(
        CreateWatchRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var uri = new Uri(request.SearchUrl);
        var provider = providers.Resolve(uri);
        var watch = new Watch
        {
            Name = request.Name.Trim(),
            SearchUrl = request.SearchUrl.Trim(),
            ProviderId = provider.Id,
            Cron = NormalizeCron(request.Cron),
            IsEnabled = request.IsEnabled,
        };
        db.Watches.Add(watch);
        await db.SaveChangesAsync(cancellationToken);
        return Map(watch);
    }

    public async Task<WatchResponse?> UpdateAsync(
        Guid id,
        UpdateWatchRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var watch = await db
            .Watches.Include(w => w.NotificationRoutes)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (watch is null)
        {
            return null;
        }

        var uri = new Uri(request.SearchUrl);
        var provider = providers.Resolve(uri);
        watch.Name = request.Name.Trim();
        watch.SearchUrl = request.SearchUrl.Trim();
        watch.ProviderId = provider.Id;
        watch.Cron = NormalizeCron(request.Cron);
        watch.IsEnabled = request.IsEnabled;
        await db.SaveChangesAsync(cancellationToken);
        return Map(watch);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var watch = await db.Watches.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (watch is null)
        {
            return false;
        }

        db.Watches.Remove(watch);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<NotificationRouteResponse?> AddRouteAsync(
        Guid watchId,
        CreateNotificationRouteRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var watch = await db.Watches.FirstOrDefaultAsync(w => w.Id == watchId, cancellationToken);
        if (watch is null)
        {
            return null;
        }

        var route = new NotificationRoute
        {
            WatchId = watchId,
            Channel = request.Channel,
            Destination = request.Destination.Trim(),
            Triggers = request.Triggers,
            IsEnabled = request.IsEnabled,
        };
        db.NotificationRoutes.Add(route);
        await db.SaveChangesAsync(cancellationToken);
        return new NotificationRouteResponse(
            route.Id,
            route.Channel,
            route.Destination,
            route.Triggers,
            route.IsEnabled
        );
    }

    public async Task<bool> DeleteRouteAsync(
        Guid watchId,
        Guid routeId,
        CancellationToken cancellationToken = default
    )
    {
        var route = await db.NotificationRoutes.FirstOrDefaultAsync(
            r => r.Id == routeId && r.WatchId == watchId,
            cancellationToken
        );
        if (route is null)
        {
            return false;
        }

        db.NotificationRoutes.Remove(route);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string NormalizeCron(string? cron)
    {
        var value = string.IsNullOrWhiteSpace(cron)
            ? WatchCronSchedule.Every6HoursCron
            : cron.Trim();
        if (!WatchCronSchedule.TryValidate(value, out var error))
        {
            throw new ArgumentException(
                string.IsNullOrWhiteSpace(error)
                    ? "Invalid cron expression."
                    : $"Invalid cron expression: {error}",
                nameof(cron)
            );
        }

        return value;
    }

    private static WatchResponse Map(Watch watch) =>
        new(
            watch.Id,
            watch.Name,
            watch.ProviderId,
            watch.SearchUrl,
            watch.Cron,
            watch.IsEnabled,
            watch.HasCompletedBaseline,
            watch.CreatedAt,
            watch.LastScannedAt,
            watch.CloudflareStrikeCount,
            watch.CloudflareBlockedUntil,
            watch
                .NotificationRoutes.OrderBy(r => r.Channel)
                .Select(r => new NotificationRouteResponse(
                    r.Id,
                    r.Channel,
                    r.Destination,
                    r.Triggers,
                    r.IsEnabled
                ))
                .ToList()
        );
}

/// <summary>Listing queries and bookmarks.</summary>
public interface IListingQueryService
{
    Task<IReadOnlyList<ListingResponse>> GetForWatchAsync(
        Guid? watchId,
        bool bookmarkedOnly,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ListingResponse>> GetNewestAsync(
        int take = 24,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ListingResponse>> GetPriceDropsAsync(
        int take = 24,
        CancellationToken cancellationToken = default
    );

    Task<ListingDetailResponse?> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );
    Task<bool> SetBookmarkAsync(
        Guid listingId,
        bool bookmarked,
        CancellationToken cancellationToken = default
    );
    Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Listing query implementation.</summary>
public sealed class ListingQueryService(IAppDbContext db) : IListingQueryService
{
    public async Task<IReadOnlyList<ListingResponse>> GetForWatchAsync(
        Guid? watchId,
        bool bookmarkedOnly,
        CancellationToken cancellationToken = default
    )
    {
        var query = db.Listings.AsNoTracking().AsQueryable();

        if (watchId is Guid wid)
        {
            query = query.Where(l => l.Sightings.Any(s => s.WatchId == wid));
        }

        if (bookmarkedOnly)
        {
            query = query.Where(l => l.Bookmark != null);
        }

        var items = await query
            .Include(l => l.Prices)
            .Include(l => l.Bookmark)
            .OrderByDescending(l => l.LastSeenAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return items.Select(l => MapListItem(l)).ToList();
    }

    public async Task<IReadOnlyList<ListingResponse>> GetNewestAsync(
        int take = 24,
        CancellationToken cancellationToken = default
    )
    {
        take = Math.Clamp(take, 1, 100);
        var items = await db
            .Listings.AsNoTracking()
            .Include(l => l.Prices)
            .Include(l => l.Bookmark)
            .OrderByDescending(l => l.FirstSeenAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return items.Select(l => MapListItem(l)).ToList();
    }

    public async Task<IReadOnlyList<ListingResponse>> GetPriceDropsAsync(
        int take = 24,
        CancellationToken cancellationToken = default
    )
    {
        take = Math.Clamp(take, 1, 100);
        var candidates = await db
            .Listings.AsNoTracking()
            .Include(l => l.Prices)
            .Include(l => l.Bookmark)
            .Where(l => l.Prices.Count >= 2)
            .ToListAsync(cancellationToken);

        return candidates
            .Select(l =>
            {
                var ordered = l.Prices.OrderByDescending(p => p.ObservedAt).Take(2).ToList();
                if (ordered.Count < 2 || ordered[0].Amount >= ordered[1].Amount)
                {
                    return (Item: (ListingResponse?)null, DroppedAt: DateTimeOffset.MinValue);
                }

                return (Item: MapListItem(l, ordered[1].Amount), DroppedAt: ordered[0].ObservedAt);
            })
            .Where(x => x.Item is not null)
            .OrderByDescending(x => x.DroppedAt)
            .Take(take)
            .Select(x => x.Item!)
            .ToList();
    }

    public async Task<ListingDetailResponse?> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var listing = await db
            .Listings.AsNoTracking()
            .Include(l => l.Prices)
            .Include(l => l.Bookmark)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (listing is null)
        {
            return null;
        }

        return new ListingDetailResponse(
            listing.Id,
            listing.ProviderId,
            listing.ExternalId,
            listing.Url,
            listing.Title,
            listing.ImageUrl,
            listing.Description,
            listing.PropertyType,
            listing.Rooms,
            listing.SizeText,
            listing.YearText,
            listing.FloorText,
            listing.Location,
            listing.LandSizeText,
            listing.Bookmark is not null,
            listing
                .Prices.OrderBy(p => p.ObservedAt)
                .Select(p => new PricePointResponse(p.ObservedAt, p.Amount, p.Currency))
                .ToList()
        );
    }

    public async Task<bool> SetBookmarkAsync(
        Guid listingId,
        bool bookmarked,
        CancellationToken cancellationToken = default
    )
    {
        var listing = await db.Listings.FirstOrDefaultAsync(
            l => l.Id == listingId,
            cancellationToken
        );
        if (listing is null)
        {
            return false;
        }

        var existing = await db.Bookmarks.FirstOrDefaultAsync(
            b => b.ListingId == listingId,
            cancellationToken
        );
        if (bookmarked && existing is null)
        {
            db.Bookmarks.Add(new Bookmark { ListingId = listingId });
        }
        else if (!bookmarked && existing is not null)
        {
            db.Bookmarks.Remove(existing);
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        var listings = await db.Listings.ToListAsync(cancellationToken);
        if (listings.Count == 0)
        {
            return 0;
        }

        db.Listings.RemoveRange(listings);
        await db.SaveChangesAsync(cancellationToken);
        return listings.Count;
    }

    private static ListingResponse MapListItem(Listing listing, decimal? previousPrice = null)
    {
        var latest = listing.Prices.OrderByDescending(p => p.ObservedAt).FirstOrDefault();
        return new ListingResponse(
            listing.Id,
            listing.ProviderId,
            listing.ExternalId,
            listing.Url,
            listing.Title,
            listing.ImageUrl,
            listing.Description,
            listing.PropertyType,
            listing.Rooms,
            listing.SizeText,
            listing.YearText,
            listing.FloorText,
            listing.Location,
            listing.LandSizeText,
            latest?.Amount,
            latest?.Currency ?? "EUR",
            listing.FirstSeenAt,
            listing.LastSeenAt,
            listing.Bookmark is not null,
            previousPrice
        );
    }
}

/// <summary>Scan history queries.</summary>
public interface IScanRunQueryService
{
    Task<IReadOnlyList<ScanRunResponse>> GetRecentAsync(
        int take = 100,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ScanRunResponse>> GetActiveAsync(
        CancellationToken cancellationToken = default
    );
}

/// <summary>Scan history query implementation.</summary>
public sealed class ScanRunQueryService(IAppDbContext db) : IScanRunQueryService
{
    public async Task<IReadOnlyList<ScanRunResponse>> GetRecentAsync(
        int take = 100,
        CancellationToken cancellationToken = default
    )
    {
        var runs = await db
            .ScanRuns.AsNoTracking()
            .Include(r => r.Watch)
            .OrderByDescending(r => r.QueuedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Map(runs);
    }

    public async Task<IReadOnlyList<ScanRunResponse>> GetActiveAsync(
        CancellationToken cancellationToken = default
    )
    {
        var runs = await db
            .ScanRuns.AsNoTracking()
            .Include(r => r.Watch)
            .Where(r => r.Status == ScanRunStatus.Queued || r.Status == ScanRunStatus.Running)
            .OrderBy(r => r.QueuedAt)
            .ToListAsync(cancellationToken);

        return Map(runs);
    }

    private static IReadOnlyList<ScanRunResponse> Map(IEnumerable<ScanRun> runs) =>
        runs.Select(r => new ScanRunResponse(
                r.Id,
                r.WatchId,
                r.Watch?.Name ?? "",
                r.Status,
                r.QueuedAt,
                r.StartedAt,
                r.FinishedAt,
                r.PagesScanned,
                r.NewCount,
                r.PriceChangeCount,
                r.ErrorSummary,
                r.NotifyErrorSummary,
                r.CloudflareBlocked
            ))
            .ToList();
}

/// <summary>Settings projection from options.</summary>
public interface ISettingsService
{
    SettingsResponse GetSettings();
}

/// <summary>Settings service implementation.</summary>
public sealed class SettingsService(IOptions<DomolovOptions> options) : ISettingsService
{
    public SettingsResponse GetSettings()
    {
        var o = options.Value;
        return new SettingsResponse(
            o.TimeZone,
            o.MaxConcurrentScans,
            o.ScanCooldownMs,
            o.CloudflareChallengeWaitMs,
            o.BrowserHeadless,
            !string.IsNullOrWhiteSpace(o.TelegramBotToken),
            !string.IsNullOrWhiteSpace(o.SmtpHost),
            !string.IsNullOrWhiteSpace(o.VapidPublicKey)
                && !string.IsNullOrWhiteSpace(o.VapidPrivateKey),
            o.VapidPublicKey,
            o.Role
        );
    }
}
