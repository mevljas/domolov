using System.Diagnostics;
using Domolov.Application.Abstractions;
using Domolov.Domain.Entities;
using Domolov.Domain.Enums;
using Domolov.Domain.Providers;
using Domolov.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domolov.Application.Services;

/// <summary>Queues ScanRuns and processes them with providers + notifiers.</summary>
public sealed class ScanOrchestrator : IScanOrchestrator
{
    public static readonly ActivitySource ActivitySource = new("Domolov.Scans");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<DomolovOptions> _options;
    private readonly ILogger<ScanOrchestrator> _logger;
    private readonly SemaphoreSlim _gate;

    public ScanOrchestrator(
        IServiceScopeFactory scopeFactory,
        IOptions<DomolovOptions> options,
        ILogger<ScanOrchestrator> logger
    )
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        var max = Math.Max(1, options.Value.MaxConcurrentScans);
        _gate = new SemaphoreSlim(max, max);
    }

    public async Task<Guid> EnqueueAsync(
        Guid watchId,
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        _ =
            await db.Watches.FirstOrDefaultAsync(w => w.Id == watchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Watch {watchId} not found.");

        var existing = await db
            .ScanRuns.Where(r =>
                r.WatchId == watchId
                && (r.Status == ScanRunStatus.Queued || r.Status == ScanRunStatus.Running)
            )
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is Guid id)
        {
            return id;
        }

        var run = new ScanRun { WatchId = watchId, Status = ScanRunStatus.Queued };
        db.ScanRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Queued ScanRun {ScanRunId} for Watch {WatchId}", run.Id, watchId);
        return run.Id;
    }

    public async Task ProcessQueuedAsync(CancellationToken cancellationToken = default)
    {
        List<Guid> queuedIds;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            queuedIds = await db
                .ScanRuns.Where(r => r.Status == ScanRunStatus.Queued)
                .OrderBy(r => r.QueuedAt)
                .Take(_options.Value.MaxConcurrentScans * 2)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);
        }

        var tasks = queuedIds.Select(id => ProcessOneAsync(id, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private async Task ProcessOneAsync(Guid scanRunId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var activity = ActivitySource.StartActivity("ProcessScan");
            activity?.SetTag("scan.id", scanRunId.ToString());

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var providers = scope.ServiceProvider.GetRequiredService<IListingProviderResolver>();
            var notifiers = scope.ServiceProvider.GetServices<INotifier>().ToList();

            var run = await db
                .ScanRuns.Include(r => r.Watch)!
                .ThenInclude(w => w!.NotificationRoutes)
                .FirstOrDefaultAsync(r => r.Id == scanRunId, cancellationToken);
            if (run?.Watch is null || run.Status != ScanRunStatus.Queued)
            {
                return;
            }

            run.Status = ScanRunStatus.Running;
            run.StartedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                await ExecuteScanAsync(db, providers, notifiers, run, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScanRun {ScanRunId} failed", scanRunId);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                run.Status = ScanRunStatus.Failed;
                run.ErrorSummary = ex.Message;
                run.FinishedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ExecuteScanAsync(
        IAppDbContext db,
        IListingProviderResolver providers,
        IReadOnlyList<INotifier> notifiers,
        ScanRun run,
        CancellationToken cancellationToken
    )
    {
        var watch = run.Watch!;
        var provider =
            providers.TryGetById(watch.ProviderId) ?? providers.Resolve(new Uri(watch.SearchUrl));
        var isBaseline = !watch.HasCompletedBaseline;
        var diffs = new List<ListingDiff>();
        var pages = 0;

        await foreach (
            var card in provider.CrawlAsync(
                new CrawlRequest(new Uri(watch.SearchUrl), run.Id),
                cancellationToken
            )
        )
        {
            pages = Math.Max(pages, card.PageIndex);
            var listing = await db
                .Listings.Include(l => l.Prices)
                .FirstOrDefaultAsync(
                    l => l.ProviderId == provider.Id && l.ExternalId == card.ExternalId,
                    cancellationToken
                );

            decimal? previous = listing
                ?.Prices.OrderByDescending(p => p.ObservedAt)
                .Select(p => (decimal?)p.Amount)
                .FirstOrDefault();

            var kind = listing is null
                ? ListingChangeKind.New
                : ListingDiffService.Classify(previous, card.Price);

            if (listing is null)
            {
                listing = new Listing
                {
                    ProviderId = provider.Id,
                    ExternalId = card.ExternalId,
                    Url = card.Url,
                    Title = card.Title,
                    ImageUrl = RewriteImageHost(card.ImageUrl),
                    Description = card.Description,
                    PropertyType = card.PropertyType,
                    Rooms = card.Rooms,
                    SizeText = card.SizeText,
                    YearText = card.YearText,
                    FloorText = card.FloorText,
                };
                db.Listings.Add(listing);
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                listing.Title = card.Title;
                listing.Url = card.Url;
                listing.ImageUrl = RewriteImageHost(card.ImageUrl) ?? listing.ImageUrl;
                listing.Description = card.Description ?? listing.Description;
                listing.PropertyType = card.PropertyType ?? listing.PropertyType;
                listing.Rooms = card.Rooms ?? listing.Rooms;
                listing.SizeText = card.SizeText ?? listing.SizeText;
                listing.YearText = card.YearText ?? listing.YearText;
                listing.FloorText = card.FloorText ?? listing.FloorText;
                listing.LastSeenAt = DateTimeOffset.UtcNow;
            }

            if (
                card.Price is decimal price
                && (
                    kind
                    is ListingChangeKind.New
                        or ListingChangeKind.PriceDecreased
                        or ListingChangeKind.PriceIncreased
                )
            )
            {
                db.PriceObservations.Add(
                    new PriceObservation
                    {
                        ListingId = listing.Id,
                        Amount = price,
                        Currency = string.IsNullOrWhiteSpace(card.Currency) ? "EUR" : card.Currency,
                    }
                );
            }
            else if (card.Price is decimal firstPrice && listing.Prices.Count == 0)
            {
                db.PriceObservations.Add(
                    new PriceObservation
                    {
                        ListingId = listing.Id,
                        Amount = firstPrice,
                        Currency = string.IsNullOrWhiteSpace(card.Currency) ? "EUR" : card.Currency,
                    }
                );
            }

            var sighting = await db.WatchSightings.FirstOrDefaultAsync(
                s => s.WatchId == watch.Id && s.ListingId == listing.Id,
                cancellationToken
            );
            if (sighting is null)
            {
                db.WatchSightings.Add(
                    new WatchSighting { WatchId = watch.Id, ListingId = listing.Id }
                );
            }
            else
            {
                sighting.LastSeenAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            diffs.Add(new ListingDiff(card, kind, previous));
        }

        run.PagesScanned = pages;
        run.NewCount = diffs.Count(d => d.Kind == ListingChangeKind.New);
        run.PriceChangeCount = diffs.Count(d =>
            d.Kind is ListingChangeKind.PriceDecreased or ListingChangeKind.PriceIncreased
        );
        run.FinishedAt = DateTimeOffset.UtcNow;
        watch.LastScannedAt = run.FinishedAt;

        if (isBaseline)
        {
            watch.HasCompletedBaseline = true;
            run.Status = ScanRunStatus.Baseline;
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Baseline ScanRun {ScanRunId} stored {Count} listings without notifications",
                run.Id,
                diffs.Count
            );
            return;
        }

        run.Status = ScanRunStatus.Succeeded;
        await db.SaveChangesAsync(cancellationToken);

        var notifyErrors = new List<string>();
        var notifierMap = notifiers
            .GroupBy(n => n.Channel)
            .ToDictionary(g => g.Key, g => g.First());
        var routes = watch.NotificationRoutes.Where(r => r.IsEnabled).ToList();

        foreach (var diff in diffs.Where(d => d.Kind != ListingChangeKind.Unchanged))
        {
            foreach (var route in routes)
            {
                if (!ListingDiffService.MatchesTrigger(route.Triggers, diff.Kind))
                {
                    continue;
                }

                if (!notifierMap.TryGetValue(route.Channel, out var notifier))
                {
                    notifyErrors.Add($"No notifier for {route.Channel}");
                    continue;
                }

                try
                {
                    await notifier.SendAsync(
                        new NotificationMessage(
                            route.Channel,
                            route.Destination,
                            diff.Card.Title,
                            BuildBody(diff),
                            diff.Card.Url,
                            RewriteImageHost(diff.Card.ImageUrl),
                            diff.Card.Price,
                            diff.PreviousPrice is null ? null : [diff.PreviousPrice.Value]
                        ),
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Notification failed for route {RouteId} channel {Channel}",
                        route.Id,
                        route.Channel
                    );
                    notifyErrors.Add($"{route.Channel}: {ex.Message}");
                }
            }
        }

        if (notifyErrors.Count > 0)
        {
            run.NotifyErrorSummary = string.Join("; ", notifyErrors.Distinct().Take(5));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string BuildBody(ListingDiff diff) =>
        diff.Kind switch
        {
            ListingChangeKind.New => "New listing",
            ListingChangeKind.PriceDecreased =>
                $"Price decreased from {diff.PreviousPrice} to {diff.Card.Price}",
            ListingChangeKind.PriceIncreased =>
                $"Price increased from {diff.PreviousPrice} to {diff.Card.Price}",
            _ => "Listing update",
        };

    private static string? RewriteImageHost(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return imageUrl;
        }

        return imageUrl.Replace(
            "img.nepremicnine.net",
            "img.onnepremicnine.net",
            StringComparison.OrdinalIgnoreCase
        );
    }
}
