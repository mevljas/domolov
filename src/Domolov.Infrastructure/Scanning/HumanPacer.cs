namespace Domolov.Infrastructure.Scanning;

/// <summary>
/// Bounded, randomised timing that makes page visits look like a person reading results.
/// Only used after real readiness signals (DOM ready, results visible); never instead of them.
/// </summary>
public sealed class HumanPacer(Random random, TimeProvider clock)
{
    public static readonly TimeSpan MinDwell = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxDwell = TimeSpan.FromSeconds(15);
    private const double MedianDwellSeconds = 8;
    private const double DwellSigma = 0.35;

    /// <summary>Log-normal time to spend on a results page, clamped to [5 s, 15 s].</summary>
    public TimeSpan DwellTime()
    {
        var normal =
            Math.Sqrt(-2 * Math.Log(1 - random.NextDouble()))
            * Math.Cos(2 * Math.PI * random.NextDouble());
        var seconds = MedianDwellSeconds * Math.Exp(DwellSigma * normal);
        return TimeSpan.FromSeconds(
            Math.Clamp(seconds, MinDwell.TotalSeconds, MaxDwell.TotalSeconds)
        );
    }

    /// <summary>A short pause between scroll steps or before a click.</summary>
    public TimeSpan ShortPause() => TimeSpan.FromMilliseconds(random.Next(120, 900));

    /// <summary>
    /// Wheel steps (pixels) that scroll roughly <paramref name="distance"/> down, with variable
    /// step sizes and occasional small scroll-backs.
    /// </summary>
    public IReadOnlyList<int> ScrollPlan(int distance)
    {
        var steps = new List<int>();
        var scrolled = 0;
        while (scrolled < distance && steps.Count < 200)
        {
            if (steps.Count > 2 && random.NextDouble() < 0.1)
            {
                var back = -random.Next(40, 121);
                steps.Add(back);
                scrolled += back;
                continue;
            }

            var step = Math.Min(random.Next(80, 401), Math.Max(80, distance - scrolled));
            steps.Add(step);
            scrolled += step;
        }

        return steps;
    }

    /// <summary>A point inside the viewport for an idle mouse movement.</summary>
    public (float X, float Y, int Steps) MouseTarget(int width, int height) =>
        (
            (float)(width * (0.15 + (random.NextDouble() * 0.7))),
            (float)(height * (0.2 + (random.NextDouble() * 0.6))),
            random.Next(8, 25)
        );

    public bool Chance(double probability) => random.NextDouble() < probability;

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, clock, cancellationToken);

    /// <summary>Scan cooldown with ±25 % jitter.</summary>
    public TimeSpan Jitter(TimeSpan baseDelay) =>
        baseDelay <= TimeSpan.Zero
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(
                baseDelay.TotalMilliseconds * (0.75 + (random.NextDouble() * 0.5))
            );
}
