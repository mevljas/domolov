using Domolov.Domain.Scans;
using FluentAssertions;

namespace Domolov.UnitTests.Domain;

public sealed class ScanRunTests
{
    private static ScanRun Running()
    {
        var run = new ScanRun(Guid.NewGuid(), isManual: true, TestData.Now);
        run.Start(TestData.Now.AddSeconds(1));
        return run;
    }

    [Fact]
    public void Successful_run_that_reached_the_end_is_complete()
    {
        var run = Running();
        run.Succeed(
            baseline: false,
            new ScanStats(3, 2, 1, 1),
            crawlReachedEnd: true,
            TestData.Now.AddMinutes(1)
        );

        run.Status.Should().Be(ScanRunStatus.Succeeded);
        run.IsComplete.Should().BeTrue();
        run.RepostCount.Should().Be(1);
    }

    [Fact]
    public void Baseline_and_truncated_runs_are_not_complete()
    {
        var baseline = Running();
        baseline.Succeed(
            baseline: true,
            new ScanStats(1, 5, 0, 0),
            crawlReachedEnd: true,
            TestData.Now
        );
        baseline.Status.Should().Be(ScanRunStatus.Baseline);
        baseline.IsComplete.Should().BeFalse();

        var truncated = Running();
        truncated.Succeed(
            baseline: false,
            new ScanStats(50, 0, 0, 0),
            crawlReachedEnd: false,
            TestData.Now
        );
        truncated.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void Only_queued_runs_can_start()
    {
        var run = Running();
        var act = () => run.Start(TestData.Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Interrupt_only_affects_active_runs()
    {
        var run = Running();
        run.Interrupt(TestData.Now.AddMinutes(2));
        run.Status.Should().Be(ScanRunStatus.Interrupted);
        run.ErrorSummary.Should().Contain("Interrupted");

        var done = Running();
        done.Succeed(false, new ScanStats(1, 0, 0, 0), true, TestData.Now);
        done.Interrupt(TestData.Now);
        done.Status.Should().Be(ScanRunStatus.Succeeded);
    }

    [Fact]
    public void Notification_errors_are_deduplicated_and_capped()
    {
        var run = Running();
        run.RecordNotificationErrors(["a", "a", "b", "c", "d", "e", "f"]);
        run.NotifyErrorSummary.Should().Be("a; b; c; d; e");
    }
}
