using Domolov.Domain.Watches;
using FluentAssertions;

namespace Domolov.UnitTests.Domain;

public sealed class WatchScheduleTests
{
    private static readonly Guid WatchId = Guid.Parse("0192f5a0-0000-7000-8000-000000000001");

    [Fact]
    public void Never_scanned_watch_is_due()
    {
        var now = DateTimeOffset.Parse("2026-08-30T12:05:00Z");
        WatchSchedule.IsDue(WatchId, "0 * * * *", null, now, TimeZoneInfo.Utc).Should().BeTrue();
    }

    [Fact]
    public void Watch_is_not_due_right_after_a_scan()
    {
        var now = DateTimeOffset.Parse("2026-08-30T12:05:00Z");
        var last = DateTimeOffset.Parse("2026-08-30T12:00:00Z");
        WatchSchedule.IsDue(WatchId, "0 * * * *", last, now, TimeZoneInfo.Utc).Should().BeFalse();
    }

    [Fact]
    public void Jitter_stays_within_twenty_percent_of_the_interval()
    {
        var occurrence = DateTimeOffset.Parse("2026-08-30T12:00:00Z");
        for (var i = 0; i < 200; i++)
        {
            var jitter = WatchSchedule.JitterFor(
                Guid.NewGuid(),
                "0 */6 * * *",
                occurrence.AddHours(6 * i),
                TimeZoneInfo.Utc
            );
            jitter
                .Should()
                .BeGreaterThanOrEqualTo(TimeSpan.Zero)
                .And.BeLessThan(TimeSpan.FromMinutes(72));
        }
    }

    [Fact]
    public void Jitter_is_deterministic_per_watch_and_occurrence()
    {
        var occurrence = DateTimeOffset.Parse("2026-08-30T12:00:00Z");
        var a = WatchSchedule.JitterFor(WatchId, "0 * * * *", occurrence, TimeZoneInfo.Utc);
        var b = WatchSchedule.JitterFor(WatchId, "0 * * * *", occurrence, TimeZoneInfo.Utc);
        a.Should().Be(b);
    }

    [Fact]
    public void Jitter_moves_starts_off_the_cron_boundary_for_most_watches()
    {
        var occurrence = DateTimeOffset.Parse("2026-08-30T12:00:00Z");
        var offBoundary = Enumerable
            .Range(0, 50)
            .Count(_ =>
                WatchSchedule.JitterFor(Guid.NewGuid(), "0 * * * *", occurrence, TimeZoneInfo.Utc)
                > TimeSpan.FromSeconds(30)
            );
        offBoundary.Should().BeGreaterThan(40);
    }

    [Fact]
    public void Next_run_includes_the_jitter()
    {
        var last = DateTimeOffset.Parse("2026-08-30T12:00:00Z");
        var next = WatchSchedule.GetNextRunAt(WatchId, "0 * * * *", last, last, TimeZoneInfo.Utc);
        next.Should().BeOnOrAfter(DateTimeOffset.Parse("2026-08-30T13:00:00Z"));
        next.Should().BeBefore(DateTimeOffset.Parse("2026-08-30T13:12:00Z"));
    }
}
