using Domolov.Application.Abstractions;
using Domolov.Domain.Enums;
using Domolov.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domolov.Infrastructure.Workers;

/// <summary>Background worker that schedules cron-due Watches and processes the queue.</summary>
public sealed class ScanSchedulerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<DomolovOptions> options,
    ILogger<ScanSchedulerWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var role = options.Value.Role;
        if (
            !string.Equals(role, "all", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role, "worker", StringComparison.OrdinalIgnoreCase)
        )
        {
            logger.LogInformation("ScanSchedulerWorker disabled for role {Role}", role);
            return;
        }

        logger.LogInformation("ScanSchedulerWorker started");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IScanOrchestrator>();
                var tz = ResolveTimeZone(options.Value.TimeZone);
                var now = DateTimeOffset.UtcNow;

                var watches = await db
                    .Watches.AsNoTracking()
                    .Where(w => w.IsEnabled)
                    .ToListAsync(stoppingToken);

                foreach (var watch in watches)
                {
                    if (WatchSchedule.IsDue(watch.Cron, watch.LastScannedAt, now, tz))
                    {
                        await orchestrator.EnqueueAsync(watch.Id, stoppingToken);
                    }
                }

                await orchestrator.ProcessQueuedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduler tick failed");
            }
        }
    }

    internal static TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows may need conversion for IANA ids on older hosts; try UTC fallback.
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
