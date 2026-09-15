using Domolov.Domain.Providers;

namespace Domolov.Domain.Services;

/// <summary>
/// Waits for a CloudflareChallenge to clear; throws on CloudflareBlock (timeout).
/// </summary>
public static class CloudflareChallengeGate
{
    public const int DefaultPollIntervalMs = 500;

    /// <summary>
    /// Reads HTML until it no longer looks like a CloudflareChallenge, or throws
    /// <see cref="CloudflareBlockedException"/> after <paramref name="waitMs"/>.
    /// </summary>
    public static async Task WaitUntilClearedOrThrowAsync(
        Func<CancellationToken, Task<string>> readHtmlAsync,
        int waitMs,
        string blockedMessage,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null,
        Func<int, CancellationToken, Task>? delayAsync = null,
        Action? onChallengeDetected = null,
        int pollIntervalMs = DefaultPollIntervalMs
    )
    {
        ArgumentNullException.ThrowIfNull(readHtmlAsync);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockedMessage);

        var clock = timeProvider ?? TimeProvider.System;
        Func<int, CancellationToken, Task> delay;
        if (delayAsync is null)
        {
            delay = (ms, ct) => Task.Delay(TimeSpan.FromMilliseconds(ms), clock, ct);
        }
        else
        {
            delay = delayAsync;
        }
        var pollMs = Math.Max(1, pollIntervalMs);

        var html = await readHtmlAsync(cancellationToken).ConfigureAwait(false);
        if (!NepremicnineParsing.LooksLikeCloudflareChallenge(html))
        {
            return;
        }

        onChallengeDetected?.Invoke();

        var wait = Math.Max(0, waitMs);
        var deadline = clock.GetUtcNow().AddMilliseconds(wait);

        while (clock.GetUtcNow() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remainingMs = (int)Math.Ceiling((deadline - clock.GetUtcNow()).TotalMilliseconds);
            if (remainingMs <= 0)
            {
                break;
            }

            var sleepMs = Math.Min(pollMs, remainingMs);
            await delay(sleepMs, cancellationToken).ConfigureAwait(false);

            html = await readHtmlAsync(cancellationToken).ConfigureAwait(false);
            if (!NepremicnineParsing.LooksLikeCloudflareChallenge(html))
            {
                return;
            }
        }

        throw new CloudflareBlockedException(blockedMessage);
    }
}
