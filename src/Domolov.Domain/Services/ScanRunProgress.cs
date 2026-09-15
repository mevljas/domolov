using Domolov.Domain.Enums;

namespace Domolov.Domain.Services;

/// <summary>Elapsed-time helpers for in-progress ScanRuns.</summary>
public static class ScanRunProgress
{
    public static bool IsActive(ScanRunStatus status) =>
        status is ScanRunStatus.Queued or ScanRunStatus.Running;

    public static DateTimeOffset GetElapsedStart(
        ScanRunStatus status,
        DateTimeOffset queuedAt,
        DateTimeOffset? startedAt
    ) =>
        status == ScanRunStatus.Running && startedAt is DateTimeOffset started ? started : queuedAt;

    public static TimeSpan GetElapsed(
        ScanRunStatus status,
        DateTimeOffset queuedAt,
        DateTimeOffset? startedAt,
        DateTimeOffset utcNow
    )
    {
        var start = GetElapsedStart(status, queuedAt, startedAt);
        var elapsed = utcNow - start;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    public static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
        }

        return $"{(int)elapsed.TotalSeconds}s";
    }
}
