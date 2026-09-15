using Domolov.Domain.Providers;
using Domolov.Domain.Services;
using FluentAssertions;

namespace Domolov.UnitTests;

public sealed class CloudflareChallengeGateTests
{
    private const string ChallengeHtml = "<html>Just a moment...</html>";
    private const string OkHtml = "<html>ok listings</html>";

    [Fact]
    public async Task WaitUntilCleared_returns_immediately_when_html_is_clear()
    {
        var reads = 0;
        var challenged = false;

        await CloudflareChallengeGate.WaitUntilClearedOrThrowAsync(
            _ =>
            {
                reads++;
                return Task.FromResult(OkHtml);
            },
            waitMs: 5_000,
            blockedMessage: "blocked",
            cancellationToken: CancellationToken.None,
            onChallengeDetected: () => challenged = true
        );

        reads.Should().Be(1);
        challenged.Should().BeFalse();
    }

    [Fact]
    public async Task WaitUntilCleared_returns_when_challenge_clears_on_later_poll()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));
        var reads = 0;
        var challenged = false;

        await CloudflareChallengeGate.WaitUntilClearedOrThrowAsync(
            _ =>
            {
                reads++;
                return Task.FromResult(reads == 1 ? ChallengeHtml : OkHtml);
            },
            waitMs: 5_000,
            blockedMessage: "blocked",
            cancellationToken: CancellationToken.None,
            timeProvider: clock,
            delayAsync: (ms, _) =>
            {
                clock.Advance(TimeSpan.FromMilliseconds(ms));
                return Task.CompletedTask;
            },
            onChallengeDetected: () => challenged = true,
            pollIntervalMs: 500
        );

        reads.Should().Be(2);
        challenged.Should().BeTrue();
    }

    [Fact]
    public async Task WaitUntilCleared_throws_CloudflareBlockedException_when_challenge_persists()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));
        var reads = 0;

        var act = async () =>
            await CloudflareChallengeGate.WaitUntilClearedOrThrowAsync(
                _ =>
                {
                    reads++;
                    return Task.FromResult(ChallengeHtml);
                },
                waitMs: 1_000,
                blockedMessage: "CloudflareBlock while warming https://example.com/",
                cancellationToken: CancellationToken.None,
                timeProvider: clock,
                delayAsync: (ms, _) =>
                {
                    clock.Advance(TimeSpan.FromMilliseconds(ms));
                    return Task.CompletedTask;
                },
                pollIntervalMs: 500
            );

        var ex = await act.Should().ThrowAsync<CloudflareBlockedException>();
        ex.Which.Message.Should().Contain("CloudflareBlock");
        reads.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task WaitUntilCleared_with_zero_wait_throws_immediately_on_challenge()
    {
        var act = async () =>
            await CloudflareChallengeGate.WaitUntilClearedOrThrowAsync(
                _ => Task.FromResult(ChallengeHtml),
                waitMs: 0,
                blockedMessage: "blocked now",
                cancellationToken: CancellationToken.None
            );

        await act.Should().ThrowAsync<CloudflareBlockedException>().WithMessage("blocked now");
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _utcNow = start;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }
}
