using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Application.Watches;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Application.Notifications;

/// <summary>Lists a Watch's routes.</summary>
public sealed class ListRoutesHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<NotificationRouteResponse>> HandleAsync(
        Guid watchId,
        CancellationToken cancellationToken
    )
    {
        if (!await db.Watches.AnyAsync(w => w.Id == watchId, cancellationToken))
        {
            throw new NotFoundException("Watch", watchId);
        }

        var routes = await db
            .NotificationRoutes.AsNoTracking()
            .Where(r => r.WatchId == watchId)
            .OrderBy(r => r.Channel)
            .ThenBy(r => r.Destination)
            .ToListAsync(cancellationToken);
        return routes.Select(NotificationRouteMapping.ToResponse).ToList();
    }
}

/// <summary>Adds a route to a Watch.</summary>
public sealed class AddRouteHandler(IAppDbContext db)
{
    public async Task<NotificationRouteResponse> HandleAsync(
        Guid watchId,
        CreateNotificationRouteRequest request,
        CancellationToken cancellationToken
    )
    {
        var watch =
            await db.Watches.FirstOrDefaultAsync(w => w.Id == watchId, cancellationToken)
            ?? throw new NotFoundException("Watch", watchId);
        var route = watch.AddRoute(
            request.Channel,
            request.Destination,
            NotificationRouteMapping.ToFlags(request.Triggers),
            request.IsEnabled
        );
        db.NotificationRoutes.Add(route);
        await db.SaveChangesAsync(cancellationToken);
        return NotificationRouteMapping.ToResponse(route);
    }
}

/// <summary>Partially updates a route (destination, triggers, enabled).</summary>
public sealed class UpdateRouteHandler(IAppDbContext db)
{
    public async Task<NotificationRouteResponse> HandleAsync(
        Guid watchId,
        Guid routeId,
        UpdateNotificationRouteRequest request,
        uint? expectedVersion,
        CancellationToken cancellationToken
    )
    {
        var route =
            await db.NotificationRoutes.FirstOrDefaultAsync(
                r => r.Id == routeId && r.WatchId == watchId,
                cancellationToken
            ) ?? throw new NotFoundException("NotificationRoute", routeId);
        Concurrency.Check(db, route, r => r.Version, expectedVersion);
        route.Update(
            request.Destination,
            request.Triggers is null ? null : NotificationRouteMapping.ToFlags(request.Triggers),
            request.IsEnabled
        );
        await UpdateWatchHandler.SaveAsync(db, cancellationToken);
        return NotificationRouteMapping.ToResponse(route);
    }
}

/// <summary>Deletes a route.</summary>
public sealed class DeleteRouteHandler(IAppDbContext db)
{
    public async Task HandleAsync(Guid watchId, Guid routeId, CancellationToken cancellationToken)
    {
        var deleted = await db
            .NotificationRoutes.Where(r => r.Id == routeId && r.WatchId == watchId)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            throw new NotFoundException("NotificationRoute", routeId);
        }
    }
}

/// <summary>Sends a fixed sample message through a route, ignoring its triggers and enabled flag.</summary>
public sealed class TestRouteHandler(IAppDbContext db, NotificationDispatcher dispatcher)
{
    public async Task<TestDeliveryResponse> HandleAsync(
        Guid watchId,
        Guid routeId,
        CancellationToken cancellationToken
    )
    {
        var route =
            await db
                .NotificationRoutes.AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.Id == routeId && r.WatchId == watchId,
                    cancellationToken
                ) ?? throw new NotFoundException("NotificationRoute", routeId);
        var error = await dispatcher.SendTestAsync(route, cancellationToken);
        return new TestDeliveryResponse(error is null, error);
    }
}
