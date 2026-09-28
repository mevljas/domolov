using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Application.Notifications;
using Domolov.Application.Scans;
using Domolov.Domain.Common;
using Domolov.Domain.Watches;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Application.Watches;

/// <summary>Lists every Watch.</summary>
public sealed class ListWatchesHandler(IAppDbContext db, WatchReader reader)
{
    public Task<IReadOnlyList<WatchResponse>> HandleAsync(CancellationToken cancellationToken) =>
        reader.ReadAsync(db.Watches, cancellationToken);
}

/// <summary>Gets one Watch.</summary>
public sealed class GetWatchHandler(IAppDbContext db, WatchReader reader)
{
    public async Task<WatchResponse> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await reader.ReadAsync(db.Watches.Where(w => w.Id == id), cancellationToken);
        return result.Count == 1 ? result[0] : throw new NotFoundException("Watch", id);
    }
}

/// <summary>Creates a Watch, optionally with its first notification route.</summary>
public sealed class CreateWatchHandler(
    IAppDbContext db,
    IListingProviderResolver providers,
    GetWatchHandler getWatch,
    ScanQueue scanQueue,
    TimeProvider clock
)
{
    public async Task<WatchResponse> HandleAsync(
        CreateWatchRequest request,
        CancellationToken cancellationToken
    )
    {
        var providerId = ProviderFor(providers, request.SearchUrl);
        var watch = new Watch(
            request.Name,
            providerId,
            request.SearchUrl,
            request.Cron ?? WatchCronSchedule.Every6HoursCron,
            clock.GetUtcNow()
        );
        if (request.IsPaused)
        {
            watch.Pause();
        }

        if (request.InitialRoute is { } route)
        {
            watch.AddRoute(
                route.Channel,
                route.Destination,
                NotificationRouteMapping.ToFlags(route.Triggers),
                route.IsEnabled
            );
        }

        db.Watches.Add(watch);
        await db.SaveChangesAsync(cancellationToken);
        if (!watch.IsPaused)
        {
            await scanQueue.EnqueueAsync(watch.Id, isManual: false, cancellationToken);
        }

        return await getWatch.HandleAsync(watch.Id, cancellationToken);
    }

    internal static string ProviderFor(IListingProviderResolver providers, string searchUrl)
    {
        if (!Uri.TryCreate(searchUrl?.Trim(), UriKind.Absolute, out var uri))
        {
            throw new DomainRuleException(
                "searchUrl",
                "Search URL must be an absolute http(s) URL."
            );
        }

        return providers.CanHandle(uri)
            ? providers.Resolve(uri).Id
            : throw new DomainRuleException(
                "searchUrl",
                "This site is not supported yet. Paste a nepremicnine.net search results URL."
            );
    }
}

/// <summary>Partially updates a Watch (name, search URL, schedule, paused).</summary>
public sealed class UpdateWatchHandler(
    IAppDbContext db,
    IListingProviderResolver providers,
    GetWatchHandler getWatch,
    TimeProvider clock
)
{
    public async Task<WatchResponse> HandleAsync(
        Guid id,
        UpdateWatchRequest request,
        uint? expectedVersion,
        CancellationToken cancellationToken
    )
    {
        var watch =
            await db.Watches.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Watch", id);
        Concurrency.Check(db, watch, w => w.Version, expectedVersion);

        if (request.Name is not null)
        {
            watch.Rename(request.Name);
        }

        if (request.Cron is not null)
        {
            watch.Reschedule(request.Cron);
        }

        if (request.SearchUrl is not null)
        {
            var providerId = CreateWatchHandler.ProviderFor(providers, request.SearchUrl);
            if (watch.ChangeSearchUrl(request.SearchUrl, providerId, clock.GetUtcNow()))
            {
                await db
                    .WatchSightings.Where(s => s.WatchId == id)
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.IsStale, true)
                                .SetProperty(x => x.MissedRunCount, 0),
                        cancellationToken
                    );
            }
        }

        if (request.IsPaused is true)
        {
            watch.Pause();
        }
        else if (request.IsPaused is false)
        {
            watch.Resume();
        }

        await SaveAsync(db, cancellationToken);
        return await getWatch.HandleAsync(id, cancellationToken);
    }

    internal static async Task SaveAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PreconditionFailedException();
        }
    }
}

/// <summary>Deletes a Watch with its routes, ScanRuns and sightings.</summary>
public sealed class DeleteWatchHandler(IAppDbContext db)
{
    public async Task HandleAsync(
        Guid id,
        uint? expectedVersion,
        CancellationToken cancellationToken
    )
    {
        var watch =
            await db.Watches.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Watch", id);
        Concurrency.Check(db, watch, w => w.Version, expectedVersion);
        db.Watches.Remove(watch);
        await UpdateWatchHandler.SaveAsync(db, cancellationToken);
    }
}

/// <summary>Queues a manual ScanRun (Run now).</summary>
public sealed class RequestScanHandler(
    IAppDbContext db,
    ScanQueue scanQueue,
    GetScanRunHandler getScanRun
)
{
    public async Task<ScanRunResponse> HandleAsync(
        Guid watchId,
        CancellationToken cancellationToken
    )
    {
        if (!await db.Watches.AnyAsync(w => w.Id == watchId, cancellationToken))
        {
            throw new NotFoundException("Watch", watchId);
        }

        var runId = await scanQueue.EnqueueAsync(watchId, isManual: true, cancellationToken);
        return await getScanRun.HandleAsync(runId!.Value, cancellationToken);
    }
}

/// <summary>Checks a pasted search URL for the create-Watch wizard.</summary>
public sealed class CheckSearchUrlHandler(IListingProviderResolver providers)
{
    public SearchUrlCheckResponse Handle(SearchUrlCheckRequest request)
    {
        if (
            !Uri.TryCreate(request.Url?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        )
        {
            return new(false, null, null, "That doesn't look like a web address.");
        }

        if (!providers.CanHandle(uri))
        {
            return new(
                false,
                null,
                null,
                "Only nepremicnine.net search URLs are supported for now."
            );
        }

        return new(true, providers.Resolve(uri).Id, SearchUrlNames.Suggest(uri), null);
    }
}
