using Domolov.Application.Abstractions;
using Domolov.Application.Options;
using Domolov.Application.Scans;
using Domolov.Domain.Watches;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domolov.Infrastructure.Workers;

/// <summary>Queues scheduled ScanRuns for Watches whose jittered cron time has come.</summary>
public sealed class ScanSchedulerService(
    IServiceScopeFactory scopeFactory,
    IOptions<DomolovOptions> options,
    TimeProvider clock,
    ILogger<ScanSchedulerService> logger
) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timeZone = options.Value.ResolveTimeZone();
        logger.LogInformation("Scan scheduler started (timezone {TimeZone})", timeZone.Id);
        using var timer = new PeriodicTimer(Tick, clock);
        do
        {
            try
            {
                await EnqueueDueAsync(timeZone, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduler tick failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EnqueueDueAsync(TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<ScanQueue>();
        var now = clock.GetUtcNow();
        var watches = await db
            .Watches.AsNoTracking()
            .Where(w => !w.IsPaused)
            .Select(w => new
            {
                w.Id,
                w.Cron,
                w.LastScannedAt,
                w.CloudflareBlockedUntil,
            })
            .ToListAsync(cancellationToken);

        foreach (var watch in watches)
        {
            if (watch.CloudflareBlockedUntil is { } blocked && blocked > now)
            {
                continue;
            }

            if (WatchSchedule.IsDue(watch.Id, watch.Cron, watch.LastScannedAt, now, timeZone))
            {
                await queue.EnqueueAsync(watch.Id, isManual: false, cancellationToken);
            }
        }
    }
}
