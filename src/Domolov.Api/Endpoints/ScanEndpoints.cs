using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Application.Scans;
using Domolov.Domain.Scans;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Domolov.Api.Endpoints;

/// <summary>ScanRuns, their artifacts and the live event stream.</summary>
public static class ScanEndpoints
{
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(25);

    public static void MapScanEndpoints(this RouteGroupBuilder api)
    {
        var scans = api.MapGroup("/scans").WithTags("Scans");

        scans
            .MapGet(
                "",
                async Task<Ok<PagedResponse<ScanRunResponse>>> (
                    Guid? watchId,
                    ScanRunStatus? status,
                    bool? active,
                    int? page,
                    int? pageSize,
                    ListScanRunsHandler h,
                    CancellationToken ct
                ) =>
                    TypedResults.Ok(
                        await h.HandleAsync(
                            new ScanRunQuery(watchId, status, active, page, pageSize),
                            ct
                        )
                    )
            )
            .WithName("ListScanRuns")
            .WithSummary("ScanRuns, newest first");

        scans
            .MapGet(
                "/events",
                (IScanRunEventHub hub, CancellationToken ct) =>
                    TypedResults.ServerSentEvents(Stream(hub, ct))
            )
            .WithName("ScanRunEvents")
            .WithSummary(
                "Server-Sent Events: 'scanRun' on every ScanRun change, 'ping' every 25 s"
            );

        scans
            .MapGet(
                "/{id:guid}",
                async Task<Ok<ScanRunResponse>> (
                    Guid id,
                    GetScanRunHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(id, ct))
            )
            .WithName("GetScanRun")
            .WithSummary("One ScanRun")
            .ProducesProblem(StatusCodes.Status404NotFound);

        scans
            .MapGet(
                "/{id:guid}/artifacts",
                async Task<Ok<IReadOnlyList<ScanArtifactResponse>>> (
                    Guid id,
                    ListScanArtifactsHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(id, ct))
            )
            .WithName("ListScanArtifacts")
            .WithSummary(
                "Diagnostics captured during a ScanRun (Cloudflare screenshots, HTML, headers)"
            );

        scans
            .MapGet(
                "/{id:guid}/artifacts/{artifactId:guid}",
                async Task<FileContentHttpResult> (
                    Guid id,
                    Guid artifactId,
                    GetScanArtifactHandler h,
                    CancellationToken ct
                ) =>
                {
                    var artifact = await h.HandleAsync(id, artifactId, ct);
                    return TypedResults.File(
                        artifact.Content,
                        artifact.ContentType,
                        artifact.FileName
                    );
                }
            )
            .WithName("GetScanArtifact")
            .WithSummary("Download an artifact");
    }

    private static async IAsyncEnumerable<SseItem<ScanRunResponse?>> Stream(
        IScanRunEventHub hub,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var channel = Channel.CreateBounded<SseItem<ScanRunResponse?>>(
            new BoundedChannelOptions(128) { FullMode = BoundedChannelFullMode.DropOldest }
        );
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linked.Token;
        var pump = Task.Run(
            async () =>
            {
                try
                {
                    await foreach (var run in hub.SubscribeAsync(token))
                    {
                        channel.Writer.TryWrite(new SseItem<ScanRunResponse?>(run, "scanRun"));
                    }
                }
                catch (OperationCanceledException)
                {
                    // Client went away.
                }
            },
            token
        );
        var ping = Task.Run(
            async () =>
            {
                try
                {
                    using var timer = new PeriodicTimer(Heartbeat);
                    while (await timer.WaitForNextTickAsync(token))
                    {
                        channel.Writer.TryWrite(new SseItem<ScanRunResponse?>(null, "ping"));
                    }
                }
                catch (OperationCanceledException)
                {
                    // Client went away.
                }
            },
            token
        );

        yield return new SseItem<ScanRunResponse?>(null, "ready");
        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(token))
            {
                yield return item;
            }
        }
        finally
        {
            await linked.CancelAsync();
            await Task.WhenAll(pump, ping).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }
}
