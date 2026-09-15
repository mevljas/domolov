using Domolov.Domain.Entities;
using Domolov.Domain.Services;
using FluentAssertions;

namespace Domolov.UnitTests;

public sealed class CloudflareBackoffTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(8, 360)]
    public void DelayForStrike_is_exponential_capped_at_six_hours(int strike, int expectedMinutes)
    {
        CloudflareBackoff.DelayForStrike(strike).Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Fact]
    public void ApplyStrike_sets_blocked_until_and_increments()
    {
        var watch = new Watch
        {
            Name = "t",
            ProviderId = "fake",
            SearchUrl = "https://example.com/",
        };
        var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");

        CloudflareBackoff.ApplyStrike(watch, now);

        watch.CloudflareStrikeCount.Should().Be(1);
        watch.CloudflareBlockedUntil.Should().Be(now.AddMinutes(5));
        CloudflareBackoff.IsBlocked(watch, now.AddMinutes(4)).Should().BeTrue();
        CloudflareBackoff.IsBlocked(watch, now.AddMinutes(6)).Should().BeFalse();
    }

    [Fact]
    public void Clear_resets_strike_state()
    {
        var watch = new Watch
        {
            Name = "t",
            ProviderId = "fake",
            SearchUrl = "https://example.com/",
            CloudflareStrikeCount = 3,
            CloudflareBlockedUntil = DateTimeOffset.UtcNow.AddHours(1),
        };

        CloudflareBackoff.Clear(watch);

        watch.CloudflareStrikeCount.Should().Be(0);
        watch.CloudflareBlockedUntil.Should().BeNull();
    }
}
