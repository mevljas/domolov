using Domolov.Domain.Enums;
using Domolov.Domain.Services;
using FluentAssertions;

namespace Domolov.UnitTests;

public sealed class ScanRunProgressTests
{
    private static readonly DateTimeOffset QueuedAt = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StartedAt = new(2026, 9, 15, 12, 0, 30, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 2, 45, TimeSpan.Zero);

    [Fact]
    public void IsActive_only_queued_and_running()
    {
        ScanRunProgress.IsActive(ScanRunStatus.Queued).Should().BeTrue();
        ScanRunProgress.IsActive(ScanRunStatus.Running).Should().BeTrue();
        ScanRunProgress.IsActive(ScanRunStatus.Succeeded).Should().BeFalse();
        ScanRunProgress.IsActive(ScanRunStatus.Failed).Should().BeFalse();
        ScanRunProgress.IsActive(ScanRunStatus.Baseline).Should().BeFalse();
    }

    [Fact]
    public void GetElapsed_queued_uses_queued_at()
    {
        var elapsed = ScanRunProgress.GetElapsed(ScanRunStatus.Queued, QueuedAt, StartedAt, Now);
        elapsed.Should().Be(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void GetElapsed_running_uses_started_at()
    {
        var elapsed = ScanRunProgress.GetElapsed(ScanRunStatus.Running, QueuedAt, StartedAt, Now);
        elapsed.Should().Be(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void GetElapsed_running_without_started_falls_back_to_queued_at()
    {
        var elapsed = ScanRunProgress.GetElapsed(ScanRunStatus.Running, QueuedAt, null, Now);
        elapsed.Should().Be(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(45));
    }

    [Theory]
    [InlineData(0, 0, 5, "5s")]
    [InlineData(0, 2, 15, "2m 15s")]
    [InlineData(1, 5, 0, "1h 5m")]
    [InlineData(3, 0, 20, "3h 0m")]
    public void FormatElapsed_compacts_span(int hours, int minutes, int seconds, string expected)
    {
        var span = new TimeSpan(hours, minutes, seconds);
        ScanRunProgress.FormatElapsed(span).Should().Be(expected);
    }
}
