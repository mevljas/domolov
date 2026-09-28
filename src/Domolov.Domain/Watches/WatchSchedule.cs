using System.Buffers.Binary;
using System.Security.Cryptography;
using Cronos;

namespace Domolov.Domain.Watches;

/// <summary>
/// Cron evaluation in a configured IANA timezone, with a deterministic start delay per
/// occurrence so scans never fire exactly on the cron boundary.
/// </summary>
public static class WatchSchedule
{
    /// <summary>Maximum start delay as a fraction of the gap to the following occurrence.</summary>
    public const double MaxJitterFraction = 0.2;

    public static DateTimeOffset? GetNextOccurrence(
        string cronExpression,
        DateTimeOffset fromUtc,
        TimeZoneInfo timeZone
    )
    {
        var cron = CronExpression.Parse(cronExpression);
        var from = DateTime.SpecifyKind(fromUtc.UtcDateTime, DateTimeKind.Utc);
        var next = cron.GetNextOccurrence(from, timeZone);
        return next is null ? null : new DateTimeOffset(next.Value, TimeSpan.Zero);
    }

    /// <summary>
    /// The jittered time the next scheduled scan should start after <paramref name="lastScannedAt"/>
    /// (or one year back when never scanned). Stable across restarts for the same inputs.
    /// </summary>
    public static DateTimeOffset? GetNextRunAt(
        Guid watchId,
        string cronExpression,
        DateTimeOffset? lastScannedAt,
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone
    )
    {
        var from = lastScannedAt ?? nowUtc.AddYears(-1);
        var occurrence = GetNextOccurrence(cronExpression, from, timeZone);
        if (occurrence is null)
        {
            return null;
        }

        return occurrence.Value + JitterFor(watchId, cronExpression, occurrence.Value, timeZone);
    }

    public static bool IsDue(
        Guid watchId,
        string cronExpression,
        DateTimeOffset? lastScannedAt,
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone
    )
    {
        var next = GetNextRunAt(watchId, cronExpression, lastScannedAt, nowUtc, timeZone);
        return next is not null && next <= nowUtc.ToUniversalTime();
    }

    /// <summary>
    /// Deterministic delay in [0, 20% of the interval to the following occurrence), seeded by
    /// Watch id and occurrence so the displayed next run time matches the actual start.
    /// </summary>
    public static TimeSpan JitterFor(
        Guid watchId,
        string cronExpression,
        DateTimeOffset occurrence,
        TimeZoneInfo timeZone
    )
    {
        var following = GetNextOccurrence(cronExpression, occurrence, timeZone);
        if (following is null)
        {
            return TimeSpan.Zero;
        }

        var maxTicks = (long)((following.Value - occurrence).Ticks * MaxJitterFraction);
        if (maxTicks <= 0)
        {
            return TimeSpan.Zero;
        }

        Span<byte> seed = stackalloc byte[24];
        watchId.TryWriteBytes(seed[..16]);
        BinaryPrimitives.WriteInt64LittleEndian(seed[16..], occurrence.UtcTicks);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(seed, digest);
        var hash = BinaryPrimitives.ReadUInt64LittleEndian(digest);
        return TimeSpan.FromTicks((long)(hash % (ulong)maxTicks));
    }
}
