using Domolov.Domain.Entities;

namespace Domolov.Domain.Services;

/// <summary>Exponential cooldown after Cloudflare blocks on a Watch.</summary>
public static class CloudflareBackoff
{
    public static readonly TimeSpan BaseDelay = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromHours(6);

    /// <summary>Delay for a 1-based strike count (after increment).</summary>
    public static TimeSpan DelayForStrike(int strikeCount)
    {
        var n = Math.Max(1, strikeCount);
        var ms = BaseDelay.TotalMilliseconds * Math.Pow(2, n - 1);
        return TimeSpan.FromMilliseconds(Math.Min(ms, MaxDelay.TotalMilliseconds));
    }

    public static bool IsBlocked(Watch watch, DateTimeOffset now) =>
        watch.CloudflareBlockedUntil is { } until && until > now;

    public static void ApplyStrike(Watch watch, DateTimeOffset now)
    {
        watch.CloudflareStrikeCount = Math.Max(0, watch.CloudflareStrikeCount) + 1;
        watch.CloudflareBlockedUntil = now.Add(DelayForStrike(watch.CloudflareStrikeCount));
    }

    public static void Clear(Watch watch)
    {
        watch.CloudflareStrikeCount = 0;
        watch.CloudflareBlockedUntil = null;
    }
}
