using Domolov.Application.Abstractions;
using Domolov.Application.Options;
using Domolov.Application.Scans;
using Domolov.Infrastructure.Scanning;
using Domolov.Infrastructure.Signals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domolov.Infrastructure.Workers;

/// <summary>
/// Runs queued ScanRuns as soon as a ScanRequested signal arrives (60 s safety poll), at most
/// MaxConcurrentScans at a time, with a jittered cooldown after each run to pace requests.
/// </summary>
public sealed class ScanQueueService(
    IServiceScopeFactory scopeFactory,
    SignalBus signals,
    HumanPacer pacer,
    IOptions<DomolovOptions> options,
    ILogger<ScanQueueService> logger
) : BackgroundService
{
    private static readonly TimeSpan SafetyPoll = TimeSpan.FromSeconds(60);
    private readonly SemaphoreSlim _wake = new(0);
    private readonly HashSet<Guid> _running = [];
    private readonly Lock _runningGate = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAsync(stoppingToken);
        _ = ListenAsync(stoppingToken);

        var max = Math.Max(1, options.Value.MaxConcurrentScans);
        using var gate = new SemaphoreSlim(max, max);
        var inFlight = new List<Task>();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<Guid> queued;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    queued = await scope
                        .ServiceProvider.GetRequiredService<ScanQueue>()
                        .PeekQueuedAsync(max * 2, stoppingToken);
                }

                foreach (var runId in queued)
                {
                    lock (_runningGate)
                    {
                        if (!_running.Add(runId))
                        {
                            continue;
                        }
                    }

                    await gate.WaitAsync(stoppingToken);
                    inFlight.Add(RunOneAsync(runId, gate, stoppingToken));
                }

                inFlight.RemoveAll(t => t.IsCompleted);
                await _wake.WaitAsync(SafetyPoll, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scan queue loop failed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        await Task.WhenAll(inFlight).ContinueWith(_ => { }, TaskScheduler.Default);
        // stoppingToken is already cancelled; the sweep must still be able to write.
        await RecoverAsync(CancellationToken.None);
    }

    private async Task RunOneAsync(Guid runId, SemaphoreSlim gate, CancellationToken stoppingToken)
    {
        try
        {
            bool ran;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                ran = await scope
                    .ServiceProvider.GetRequiredService<ScanExecutor>()
                    .RunAsync(runId, stoppingToken);
            }

            if (ran)
            {
                await pacer.DelayAsync(
                    pacer.Jitter(TimeSpan.FromMilliseconds(options.Value.ScanCooldownMs)),
                    stoppingToken
                );
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; the executor marked the run Interrupted.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ScanRun {ScanRunId} crashed", runId);
        }
        finally
        {
            lock (_runningGate)
            {
                _running.Remove(runId);
            }

            gate.Release();
            _wake.Release();
        }
    }

    private async Task RecoverAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var recovered = await scope
                .ServiceProvider.GetRequiredService<ScanQueue>()
                .RecoverInterruptedAsync(stoppingToken);
            if (recovered > 0)
            {
                logger.LogWarning("Recovered {Count} interrupted ScanRuns", recovered);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not recover interrupted ScanRuns");
        }
    }

    private async Task ListenAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var signal in signals.SubscribeAsync(stoppingToken))
            {
                if (signal.Channel == SignalChannels.ScanRequested)
                {
                    _wake.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopping.
        }
    }

    public override void Dispose()
    {
        _wake.Dispose();
        base.Dispose();
    }
}
