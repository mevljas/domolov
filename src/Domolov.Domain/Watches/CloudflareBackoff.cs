namespace Domolov.Domain.Watches;

/// <summary>Exponential cooldown applied to a Watch after a CloudflareBlock.</summary>
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
}
