using System.Runtime.CompilerServices;
using Domolov.Application.Abstractions;
using Domolov.Application.Scans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Domolov.Infrastructure.Signals;

/// <summary>Turns ScanRunChanged signals into ScanRun snapshots for SSE subscribers.</summary>
public sealed class ScanRunEventHub(
    SignalBus bus,
    IServiceScopeFactory scopeFactory,
    ILogger<ScanRunEventHub> logger
) : IScanRunEventHub
{
    public async IAsyncEnumerable<ScanRunResponse> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await foreach (var signal in bus.SubscribeAsync(cancellationToken))
        {
            if (
                signal.Channel != SignalChannels.ScanRunChanged
                || !Guid.TryParse(signal.Payload, out var runId)
            )
            {
                continue;
            }

            ScanRunResponse? run = null;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<GetScanRunHandler>();
                run = await handler.HandleAsync(runId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Could not load ScanRun {ScanRunId} for SSE", runId);
            }

            if (run is not null)
            {
                yield return run;
            }
        }
    }
}
