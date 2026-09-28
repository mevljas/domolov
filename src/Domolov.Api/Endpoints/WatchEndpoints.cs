using Domolov.Application.Notifications;
using Domolov.Application.Scans;
using Domolov.Application.Watches;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Domolov.Api.Endpoints;

/// <summary>Watches and their notification routes.</summary>
public static class WatchEndpoints
{
    public static void MapWatchEndpoints(this RouteGroupBuilder api)
    {
        var watches = api.MapGroup("/watches").WithTags("Watches");

        watches
            .MapGet(
                "",
                async Task<Ok<IReadOnlyList<WatchResponse>>> (
                    ListWatchesHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(ct))
            )
            .WithName("ListWatches")
            .WithSummary("List Watches with their routes, last scan and next run");

        watches
            .MapGet(
                "/{id:guid}",
                async Task<Ok<WatchResponse>> (
                    Guid id,
                    HttpResponse response,
                    GetWatchHandler h,
                    CancellationToken ct
                ) =>
                {
                    var watch = await h.HandleAsync(id, ct);
                    EntityTags.Set(response, watch.Version);
                    return TypedResults.Ok(watch);
                }
            )
            .WithName("GetWatch")
            .WithSummary("Get a Watch (ETag for conditional updates)")
            .ProducesProblem(StatusCodes.Status404NotFound);

        watches
            .MapPost(
                "",
                async Task<Created<WatchResponse>> (
                    CreateWatchRequest request,
                    CreateWatchHandler h,
                    CancellationToken ct
                ) =>
                {
                    var watch = await h.HandleAsync(request, ct);
                    return TypedResults.Created($"/api/watches/{watch.Id}", watch);
                }
            )
            .WithName("CreateWatch")
            .WithSummary(
                "Create a Watch (optionally with its first notification route); the first scan is queued"
            )
            .ProducesValidationProblem();

        watches
            .MapPatch(
                "/{id:guid}",
                async Task<Ok<WatchResponse>> (
                    Guid id,
                    UpdateWatchRequest request,
                    HttpRequest http,
                    HttpResponse response,
                    UpdateWatchHandler h,
                    CancellationToken ct
                ) =>
                {
                    var watch = await h.HandleAsync(id, request, EntityTags.IfMatch(http), ct);
                    EntityTags.Set(response, watch.Version);
                    return TypedResults.Ok(watch);
                }
            )
            .WithName("UpdateWatch")
            .WithSummary("Rename, reschedule, change the search URL, or pause/resume a Watch")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        watches
            .MapDelete(
                "/{id:guid}",
                async Task<NoContent> (
                    Guid id,
                    HttpRequest http,
                    DeleteWatchHandler h,
                    CancellationToken ct
                ) =>
                {
                    await h.HandleAsync(id, EntityTags.IfMatch(http), ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("DeleteWatch")
            .WithSummary("Delete a Watch with its routes, scans and sightings")
            .ProducesProblem(StatusCodes.Status404NotFound);

        watches
            .MapPost(
                "/{id:guid}/scans",
                async Task<Accepted<ScanRunResponse>> (
                    Guid id,
                    RequestScanHandler h,
                    CancellationToken ct
                ) =>
                {
                    var run = await h.HandleAsync(id, ct);
                    return TypedResults.Accepted($"/api/scans/{run.Id}", run);
                }
            )
            .WithName("RunWatchNow")
            .WithSummary("Queue a scan now (Run now); returns the active ScanRun")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost(
                "/search-url-checks",
                Ok<SearchUrlCheckResponse> (
                    SearchUrlCheckRequest request,
                    CheckSearchUrlHandler h
                ) => TypedResults.Ok(h.Handle(request))
            )
            .WithTags("Watches")
            .WithName("CheckSearchUrl")
            .WithSummary("Check whether a pasted search URL is supported and suggest a Watch name");
    }

    public static void MapNotificationRouteEndpoints(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/watches/{watchId:guid}/notification-routes")
            .WithTags("Notification routes");

        routes
            .MapGet(
                "",
                async Task<Ok<IReadOnlyList<NotificationRouteResponse>>> (
                    Guid watchId,
                    ListRoutesHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(watchId, ct))
            )
            .WithName("ListNotificationRoutes")
            .WithSummary("List a Watch's notification routes");

        routes
            .MapPost(
                "",
                async Task<Created<NotificationRouteResponse>> (
                    Guid watchId,
                    CreateNotificationRouteRequest request,
                    AddRouteHandler h,
                    CancellationToken ct
                ) =>
                {
                    var route = await h.HandleAsync(watchId, request, ct);
                    return TypedResults.Created(
                        $"/api/watches/{watchId}/notification-routes/{route.Id}",
                        route
                    );
                }
            )
            .WithName("CreateNotificationRoute")
            .WithSummary("Add a notification route")
            .ProducesValidationProblem();

        routes
            .MapPatch(
                "/{routeId:guid}",
                async Task<Ok<NotificationRouteResponse>> (
                    Guid watchId,
                    Guid routeId,
                    UpdateNotificationRouteRequest request,
                    HttpRequest http,
                    HttpResponse response,
                    UpdateRouteHandler h,
                    CancellationToken ct
                ) =>
                {
                    var route = await h.HandleAsync(
                        watchId,
                        routeId,
                        request,
                        EntityTags.IfMatch(http),
                        ct
                    );
                    EntityTags.Set(response, route.Version);
                    return TypedResults.Ok(route);
                }
            )
            .WithName("UpdateNotificationRoute")
            .WithSummary("Change a route's destination, triggers or enabled flag")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        routes
            .MapDelete(
                "/{routeId:guid}",
                async Task<NoContent> (
                    Guid watchId,
                    Guid routeId,
                    DeleteRouteHandler h,
                    CancellationToken ct
                ) =>
                {
                    await h.HandleAsync(watchId, routeId, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("DeleteNotificationRoute")
            .WithSummary("Delete a route");

        routes
            .MapPost(
                "/{routeId:guid}/test-deliveries",
                async Task<Ok<TestDeliveryResponse>> (
                    Guid watchId,
                    Guid routeId,
                    TestRouteHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(watchId, routeId, ct))
            )
            .WithName("SendTestNotification")
            .WithSummary("Send a sample notification through the route right now");
    }
}
