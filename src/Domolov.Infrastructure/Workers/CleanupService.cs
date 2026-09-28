using Domolov.Application.Abstractions;
using Domolov.Application.Options;
using Domolov.Application.Retention;
using Domolov.Infrastructure.Signals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domolov.Infrastructure.Workers;

/// <summary>Runs the retention cleanup daily (plus up to 30 min jitter) and on request.</summary>
public sealed class CleanupService(
    IServiceScopeFactory scopeFactory,
    SignalBus signals,
    IOptions<DomolovOptions> options,
    IOptions<RetentionOptions> retention,
    TimeProvider clock,
    ILogger<CleanupService> logger
) : BackgroundService
{
    private readonly SemaphoreSlim _manual = new(0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = ListenAsync(stoppingToken);
        var timeZone = options.Value.ResolveTimeZone();
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = CleanupSchedule
                .Next(retention.Value.DailyAt, clock.GetUtcNow(), timeZone)
                .AddMinutes(Random.Shared.Next(0, 30));
            var wait = next - clock.GetUtcNow();
            bool manual;
            try
            {
                manual = await _manual.WaitAsync(
                    wait > TimeSpan.Zero ? wait : TimeSpan.Zero,
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunAsync(manual, stoppingToken);
        }
    }

    private async Task RunAsync(bool manual, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var cleaner = scope.ServiceProvider.GetRequiredService<RetentionCleaner>();
            await cleaner.RunAsync(
                manual,
                ProfileBytes(options.Value.BrowserUserDataDir),
                stoppingToken
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Cleanup crashed");
        }
    }

    private static long? ProfileBytes(string directory)
    {
        try
        {
            return Directory.Exists(directory)
                ? new DirectoryInfo(directory)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length)
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task ListenAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var signal in signals.SubscribeAsync(stoppingToken))
            {
                if (signal.Channel == SignalChannels.CleanupRequested)
                {
                    _manual.Release();
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
        _manual.Dispose();
        base.Dispose();
    }
}
