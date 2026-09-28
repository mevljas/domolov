using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Domain.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domolov.Application.Scans;

/// <summary>Queues ScanRuns and recovers runs orphaned by a crash.</summary>
public sealed class ScanQueue(
    IAppDbContext db,
    ISignalPublisher signals,
    TimeProvider clock,
    ILogger<ScanQueue> logger
)
{
    /// <summary>
    /// Queues a ScanRun unless one is already active (then returns it). Scheduled requests are
    /// skipped while the Watch is Cloudflare-blocked; manual requests clear the backoff first.
    /// </summary>
    public async Task<Guid?> EnqueueAsync(
        Guid watchId,
        bool isManual,
        CancellationToken cancellationToken
    )
    {
        var watch =
            await db.Watches.FirstOrDefaultAsync(w => w.Id == watchId, cancellationToken)
            ?? throw new NotFoundException("Watch", watchId);
        var now = clock.GetUtcNow();
        if (isManual)
        {
            watch.ClearCloudflareBackoff();
        }
        else if (watch.IsCloudflareBlocked(now))
        {
            logger.LogInformation(
                "Skipping enqueue for Watch {WatchId}; Cloudflare blocked until {Until}",
                watchId,
                watch.CloudflareBlockedUntil
            );
            return null;
        }

        var existing = await db
            .ScanRuns.Where(r =>
                r.WatchId == watchId
                && (r.Status == ScanRunStatus.Queued || r.Status == ScanRunStatus.Running)
            )
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is Guid id)
        {
            await db.SaveChangesAsync(cancellationToken);
            return id;
        }

        var run = new ScanRun(watchId, isManual, now);
        db.ScanRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Queued ScanRun {ScanRunId} for Watch {WatchId}", run.Id, watchId);
        await PublishAsync(SignalChannels.ScanRequested, run.Id, cancellationToken);
        await PublishAsync(SignalChannels.ScanRunChanged, run.Id, cancellationToken);
        return run.Id;
    }

    /// <summary>Marks Running ScanRuns as Interrupted so their Watches can be scanned again.</summary>
    public async Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken)
    {
        var running = await db
            .ScanRuns.Where(r => r.Status == ScanRunStatus.Running)
            .ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        foreach (var run in running)
        {
            run.Interrupt(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var run in running)
        {
            logger.LogWarning("Marked orphaned ScanRun {ScanRunId} as Interrupted", run.Id);
            await PublishAsync(SignalChannels.ScanRunChanged, run.Id, cancellationToken);
        }

        return running.Count;
    }

    public async Task<IReadOnlyList<Guid>> PeekQueuedAsync(
        int take,
        CancellationToken cancellationToken
    ) =>
        await db
            .ScanRuns.AsNoTracking()
            .Where(r => r.Status == ScanRunStatus.Queued)
            .OrderBy(r => r.QueuedAt)
            .Select(r => r.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    private async Task PublishAsync(string channel, Guid runId, CancellationToken cancellationToken)
    {
        try
        {
            await signals.PublishAsync(channel, runId.ToString(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Could not publish {Channel} for ScanRun {ScanRunId}",
                channel,
                runId
            );
        }
    }
}
