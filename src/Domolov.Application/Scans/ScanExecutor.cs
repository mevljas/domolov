using System.Diagnostics;
using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Application.Homes;
using Domolov.Application.Notifications;
using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using Domolov.Domain.Providers;
using Domolov.Domain.Scans;
using Domolov.Domain.Watches;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domolov.Application.Scans;

/// <summary>Runs one queued ScanRun end to end.</summary>
public sealed class ScanExecutor(
    IAppDbContext db,
    IListingProviderResolver providers,
    HomeMatcher matcher,
    HomeLinker linker,
    NotificationDispatcher dispatcher,
    ISignalPublisher signals,
    TimeProvider clock,
    ILogger<ScanExecutor> logger
)
{
    /// <summary>Executes the ScanRun if it is still queued. Returns false when it was skipped.</summary>
    public async Task<bool> RunAsync(Guid scanRunId, CancellationToken cancellationToken)
    {
        var run = await db
            .ScanRuns.Include(r => r.Watch!)
            .ThenInclude(w => w.NotificationRoutes)
            .FirstOrDefaultAsync(r => r.Id == scanRunId, cancellationToken);
        if (run?.Watch is null || run.Status != ScanRunStatus.Queued)
        {
            return false;
        }

        using var activity = DomolovTelemetry.ActivitySource.StartActivity("ScanRun");
        activity?.SetTag("scan.id", scanRunId);
        activity?.SetTag("watch.id", run.WatchId);

        var watch = run.Watch;
        run.Start(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await PublishChangedAsync(run.Id);

        var state = new ScanState(isBaseline: !watch.HasCompletedBaseline);
        var provider =
            providers.TryGetById(watch.ProviderId) ?? providers.Resolve(new Uri(watch.SearchUrl));
        var request = new CrawlRequest(new Uri(watch.SearchUrl), run.Id)
        {
            OnArtifact = (artifact, ct) => SaveArtifactAsync(run.Id, artifact, ct),
        };
        var started = Stopwatch.GetTimestamp();

        try
        {
            var page = new List<ListingCard>();
            var pageIndex = 1;
            await foreach (var card in provider.CrawlAsync(request, cancellationToken))
            {
                if (card.PageIndex != pageIndex && page.Count > 0)
                {
                    await ProcessPageAsync(watch, provider.Id, page, state, cancellationToken);
                    page.Clear();
                }

                pageIndex = card.PageIndex;
                state.Pages = Math.Max(state.Pages, card.PageIndex);
                page.Add(card);
            }

            if (page.Count > 0)
            {
                await ProcessPageAsync(watch, provider.Id, page, state, cancellationToken);
            }

            await FinishSuccessAsync(run, watch, state, request.Progress, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            run.Interrupt(clock.GetUtcNow());
            await db.SaveChangesAsync(CancellationToken.None);
            await PublishChangedAsync(run.Id);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ScanRun {ScanRunId} failed", run.Id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            var now = clock.GetUtcNow();
            var blocked = ex is CloudflareBlockedException;
            run.Fail(ex.Message, blocked, state.Pages, now);
            if (blocked)
            {
                watch.ApplyCloudflareStrike(now);
                logger.LogWarning(
                    "Watch {WatchId} Cloudflare strike {Strike}; blocked until {Until}",
                    watch.Id,
                    watch.CloudflareStrikeCount,
                    watch.CloudflareBlockedUntil
                );
            }

            await db.SaveChangesAsync(CancellationToken.None);
            await PublishChangedAsync(run.Id);
        }
        finally
        {
            var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
            DomolovTelemetry.ScanDuration.Record(seconds);
            DomolovTelemetry.ScanRuns.Add(
                1,
                new KeyValuePair<string, object?>("status", run.Status.ToString())
            );
        }

        return true;
    }

    private async Task ProcessPageAsync(
        Watch watch,
        string providerId,
        List<ListingCard> cards,
        ScanState state,
        CancellationToken cancellationToken
    )
    {
        var now = clock.GetUtcNow();
        var unique = cards
            .GroupBy(c => c.ExternalId, StringComparer.Ordinal)
            .Select(g => g.First())
            .Where(c => state.SeenExternalIds.Add(c.ExternalId))
            .ToList();
        if (unique.Count == 0)
        {
            return;
        }

        var externalIds = unique.Select(c => c.ExternalId).ToList();
        var existing = await db
            .Listings.Where(l => l.ProviderId == providerId && externalIds.Contains(l.ExternalId))
            .ToDictionaryAsync(l => l.ExternalId, StringComparer.Ordinal, cancellationToken);
        var existingIds = existing.Values.Select(l => l.Id).ToList();
        var sightings = await db
            .WatchSightings.Where(s => s.WatchId == watch.Id && existingIds.Contains(s.ListingId))
            .ToDictionaryAsync(s => s.ListingId, cancellationToken);

        var created = new List<Listing>();
        var touchedHomeIds = new HashSet<Guid>();
        foreach (var card in unique)
        {
            Listing listing;
            if (existing.TryGetValue(card.ExternalId, out var found))
            {
                listing = found;
                listing.Refresh(card, now);
                if (
                    listing.RecordPrice(card.Price, card.Currency, now)
                    != ListingChangeKind.Unchanged
                )
                {
                    state.PriceChanges++;
                }

                touchedHomeIds.Add(listing.HomeId);
            }
            else
            {
                var home = new Home(now);
                db.Homes.Add(home);
                listing = Listing.Create(home.Id, providerId, card, now);
                db.Listings.Add(listing);
                created.Add(listing);
            }

            if (sightings.TryGetValue(listing.Id, out var sighting))
            {
                sighting.Seen(now);
            }
            else
            {
                db.WatchSightings.Add(new WatchSighting(watch.Id, listing.Id, now));
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var reposted = new Dictionary<Guid, HomeEvent>();
        var newOnPage = 0;
        foreach (var listing in created)
        {
            var outcome = await matcher.MatchAsync(listing, cancellationToken);
            if (outcome.LinkedHome is { } home)
            {
                touchedHomeIds.Add(home.Id);
                if (outcome.Role == HomeListingRole.Repost)
                {
                    state.Reposts++;
                    reposted[home.Id] = new HomeEvent(
                        ListingChangeKind.Reposted,
                        home,
                        listing,
                        outcome.PreviousPrice,
                        outcome.PreviousAdEndedAt
                    );
                }
            }
            else
            {
                newOnPage++;
                touchedHomeIds.Add(listing.HomeId);
                state.NewListingIds.Add(listing.Id);
            }
        }

        state.New += newOnPage;
        await RefreshHomesAsync(touchedHomeIds, reposted, state, cancellationToken);
        DomolovTelemetry.ListingChanges.Add(
            newOnPage,
            new KeyValuePair<string, object?>("kind", "new")
        );
    }

    private async Task RefreshHomesAsync(
        HashSet<Guid> homeIds,
        Dictionary<Guid, HomeEvent> reposted,
        ScanState state,
        CancellationToken cancellationToken
    )
    {
        if (homeIds.Count == 0)
        {
            return;
        }

        var homes = await db
            .Homes.Include(h => h.Bookmark)
            .Where(h => homeIds.Contains(h.Id))
            .ToListAsync(cancellationToken);
        var listings = await db
            .Listings.Where(l => homeIds.Contains(l.HomeId))
            .ToListAsync(cancellationToken);
        var byHome = listings.ToLookup(l => l.HomeId);
        var now = clock.GetUtcNow();

        foreach (var home in homes)
        {
            var homeListings = byHome[home.Id].ToList();
            var priceBefore = home.CurrentPrice;
            var change = home.Refresh(homeListings, now);
            var primary = homeListings.First(l => l.Id == home.PrimaryListingId);

            if (reposted.TryGetValue(home.Id, out var repost))
            {
                state.Events[home.Id] = repost;
            }
            else if (homeListings.Any(l => state.NewListingIds.Contains(l.Id)))
            {
                state.Events[home.Id] = new HomeEvent(ListingChangeKind.New, home, primary, null);
            }
            else if (change != HomePriceChange.None && !state.Events.ContainsKey(home.Id))
            {
                var kind =
                    change == HomePriceChange.Decreased
                        ? ListingChangeKind.PriceDecreased
                        : ListingChangeKind.PriceIncreased;
                state.Events[home.Id] = new HomeEvent(kind, home, primary, priceBefore);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task FinishSuccessAsync(
        ScanRun run,
        Watch watch,
        ScanState state,
        CrawlProgress progress,
        CancellationToken cancellationToken
    )
    {
        var now = clock.GetUtcNow();
        var stats = new ScanStats(
            Math.Max(state.Pages, progress.PagesFetched),
            state.New,
            state.PriceChanges,
            state.Reposts
        );
        run.Succeed(state.IsBaseline, stats, progress.ReachedEnd, now);
        watch.RecordScanFinished(now);
        watch.ClearCloudflareBackoff();
        if (state.IsBaseline)
        {
            watch.CompleteBaseline();
        }

        await db.SaveChangesAsync(cancellationToken);

        if (run.IsComplete)
        {
            await ApplyDelistingAsync(watch.Id, run.StartedAt ?? now, cancellationToken);
        }

        if (!state.IsBaseline && state.Events.Count > 0)
        {
            var errors = await dispatcher.DispatchAsync(
                watch.NotificationRoutes,
                state.Events.Values.ToList(),
                cancellationToken
            );
            if (errors.Count > 0)
            {
                DomolovTelemetry.NotificationFailures.Add(errors.Count);
                run.RecordNotificationErrors(errors);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        logger.LogInformation(
            "ScanRun {ScanRunId} {Status}: {Pages} pages, {New} new, {Changes} price changes, {Reposts} reposts",
            run.Id,
            run.Status,
            stats.PagesScanned,
            stats.NewCount,
            stats.PriceChangeCount,
            stats.RepostCount
        );
        await PublishChangedAsync(run.Id);
    }

    /// <summary>
    /// After a Complete ScanRun, counts a miss for every current sighting the run did not refresh
    /// and delists Listings that every Watch has now missed twice.
    /// </summary>
    private async Task ApplyDelistingAsync(
        Guid watchId,
        DateTimeOffset runStartedAt,
        CancellationToken cancellationToken
    )
    {
        await db
            .WatchSightings.Where(s =>
                s.WatchId == watchId && !s.IsStale && s.LastSeenAt < runStartedAt
            )
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.MissedRunCount, x => x.MissedRunCount + 1),
                cancellationToken
            );

        var candidateIds = await db
            .WatchSightings.Where(s =>
                s.WatchId == watchId
                && !s.IsStale
                && s.MissedRunCount >= DelistingPolicy.MissedRunThreshold
            )
            .Select(s => s.ListingId)
            .ToListAsync(cancellationToken);
        if (candidateIds.Count == 0)
        {
            return;
        }

        var sightings = await db
            .WatchSightings.AsNoTracking()
            .Where(s => candidateIds.Contains(s.ListingId))
            .ToListAsync(cancellationToken);
        var toDelist = sightings
            .GroupBy(s => s.ListingId)
            .Where(g => DelistingPolicy.ShouldDelist(g))
            .Select(g => g.Key)
            .ToList();
        var listings = await db
            .Listings.Where(l => toDelist.Contains(l.Id) && l.DelistedAt == null)
            .ToListAsync(cancellationToken);
        if (listings.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        foreach (var listing in listings)
        {
            listing.Delist(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        var homes = await db
            .Homes.Where(h => listings.Select(l => l.HomeId).Contains(h.Id))
            .ToListAsync(cancellationToken);
        foreach (var home in homes)
        {
            await linker.RefreshAsync(home, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Delisted {Count} Listings for Watch {WatchId}",
            listings.Count,
            watchId
        );
    }

    private async Task SaveArtifactAsync(
        Guid runId,
        CrawlArtifact artifact,
        CancellationToken cancellationToken
    )
    {
        try
        {
            db.ScanRunArtifacts.Add(
                new ScanRunArtifact(
                    runId,
                    artifact.Kind,
                    artifact.Label,
                    artifact.ContentType,
                    artifact.Content,
                    clock.GetUtcNow()
                )
            );
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Could not store {Kind} artifact for ScanRun {ScanRunId}",
                artifact.Kind,
                runId
            );
        }
    }

    private async Task PublishChangedAsync(Guid runId)
    {
        try
        {
            await signals.PublishAsync(
                SignalChannels.ScanRunChanged,
                runId.ToString(),
                CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not publish ScanRun change for {ScanRunId}", runId);
        }
    }

    private sealed class ScanState(bool isBaseline)
    {
        public bool IsBaseline { get; } = isBaseline;
        public int Pages { get; set; }
        public int New { get; set; }
        public int PriceChanges { get; set; }
        public int Reposts { get; set; }
        public HashSet<string> SeenExternalIds { get; } = new(StringComparer.Ordinal);
        public HashSet<Guid> NewListingIds { get; } = [];
        public Dictionary<Guid, HomeEvent> Events { get; } = [];
    }
}
