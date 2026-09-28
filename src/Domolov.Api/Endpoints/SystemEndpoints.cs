using Domolov.Application.Dashboard;
using Domolov.Application.Push;
using Domolov.Application.Retention;
using Domolov.Application.Settings;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Domolov.Api.Endpoints;

/// <summary>Dashboard, settings, storage and push subscriptions.</summary>
public static class SystemEndpoints
{
    public static void MapSystemEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet(
                "/dashboard",
                async Task<Ok<DashboardResponse>> (GetDashboardHandler h, CancellationToken ct) =>
                    TypedResults.Ok(await h.HandleAsync(ct))
            )
            .WithTags("Dashboard")
            .WithName("GetDashboard")
            .WithSummary(
                "Headline numbers, Unseen Homes, price drops, Watch health and active scans"
            );

        api.MapGet(
                "/settings",
                Ok<SettingsResponse> (GetSettingsHandler h) => TypedResults.Ok(h.Handle())
            )
            .WithTags("Settings")
            .WithName("GetSettings")
            .WithSummary("Read-only server configuration and capability flags");

        var storage = api.MapGroup("/storage").WithTags("Storage");

        storage
            .MapGet(
                "",
                async Task<Ok<StorageResponse>> (GetStorageHandler h, CancellationToken ct) =>
                    TypedResults.Ok(await h.HandleAsync(ct))
            )
            .WithName("GetStorage")
            .WithSummary("Database size, largest tables and cleanup status");

        storage
            .MapPost(
                "/cleanups",
                async Task<Accepted> (RequestCleanupHandler h, CancellationToken ct) =>
                {
                    await h.HandleAsync(ct);
                    return TypedResults.Accepted("/api/storage");
                }
            )
            .WithName("RunCleanupNow")
            .WithSummary("Ask the worker to run the retention cleanup now");

        var push = api.MapGroup("/push-subscriptions").WithTags("Push");

        push.MapPost(
                "",
                async Task<Ok<PushSubscriptionResponse>> (
                    PushSubscriptionRequest request,
                    HttpRequest http,
                    UpsertPushSubscriptionHandler h,
                    CancellationToken ct
                ) =>
                    TypedResults.Ok(
                        await h.HandleAsync(request, http.Headers.UserAgent.ToString(), ct)
                    )
            )
            .WithName("SubscribePush")
            .WithSummary("Store this browser's Web Push subscription")
            .ProducesValidationProblem();

        push.MapDelete(
                "/{id:guid}",
                async Task<NoContent> (
                    Guid id,
                    DeletePushSubscriptionHandler h,
                    CancellationToken ct
                ) =>
                {
                    await h.HandleAsync(id, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("UnsubscribePush")
            .WithSummary("Remove a Web Push subscription");
    }
}
