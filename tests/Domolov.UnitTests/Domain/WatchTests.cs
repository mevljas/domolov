using Domolov.Domain.Common;
using Domolov.Domain.Notifications;
using Domolov.Domain.Watches;
using FluentAssertions;

namespace Domolov.UnitTests.Domain;

public sealed class WatchTests
{
    private static Watch NewWatch() =>
        new(
            "Ljubljana flats",
            "nepremicnine",
            "https://www.nepremicnine.net/oglasi-prodaja/",
            "0 */6 * * *",
            TestData.Now
        );

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(8, 360)]
    public void Cloudflare_backoff_is_exponential_and_capped_at_six_hours(
        int strike,
        int minutes
    ) => CloudflareBackoff.DelayForStrike(strike).Should().Be(TimeSpan.FromMinutes(minutes));

    [Fact]
    public void Strike_blocks_until_the_backoff_ends_and_clear_resets_it()
    {
        var watch = NewWatch();

        watch.ApplyCloudflareStrike(TestData.Now);

        watch.CloudflareStrikeCount.Should().Be(1);
        watch.IsCloudflareBlocked(TestData.Now.AddMinutes(4)).Should().BeTrue();
        watch.IsCloudflareBlocked(TestData.Now.AddMinutes(6)).Should().BeFalse();
        watch.ClearCloudflareBackoff();
        watch.CloudflareStrikeCount.Should().Be(0);
        watch.CloudflareBlockedUntil.Should().BeNull();
    }

    [Fact]
    public void Changing_the_search_url_restarts_the_baseline()
    {
        var watch = NewWatch();
        watch.CompleteBaseline();

        var changed = watch.ChangeSearchUrl(
            "https://www.nepremicnine.net/oglasi-oddaja/",
            "nepremicnine",
            TestData.Now.AddDays(1)
        );

        changed.Should().BeTrue();
        watch.HasCompletedBaseline.Should().BeFalse();
        watch.SearchUrlChangedAt.Should().Be(TestData.Now.AddDays(1));
    }

    [Fact]
    public void Same_search_url_is_not_a_change()
    {
        var watch = NewWatch();
        watch.CompleteBaseline();

        watch
            .ChangeSearchUrl(
                " https://www.nepremicnine.net/oglasi-prodaja/ ",
                "nepremicnine",
                TestData.Now
            )
            .Should()
            .BeFalse();
        watch.HasCompletedBaseline.Should().BeTrue();
    }

    [Fact]
    public void Pause_and_resume_toggle_the_paused_state()
    {
        var watch = NewWatch();
        watch.Pause();
        watch.IsPaused.Should().BeTrue();
        watch.Resume();
        watch.IsPaused.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_is_rejected(string name)
    {
        var act = () => new Watch(name, "p", "https://example.com/", "0 * * * *", TestData.Now);
        act.Should().Throw<DomainRuleException>().Which.Field.Should().Be("name");
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/")]
    public void Invalid_search_url_is_rejected(string url)
    {
        var act = () => new Watch("x", "p", url, "0 * * * *", TestData.Now);
        act.Should().Throw<DomainRuleException>().Which.Field.Should().Be("searchUrl");
    }

    [Fact]
    public void Invalid_cron_is_rejected()
    {
        var act = () => NewWatch().Reschedule("every day please");
        act.Should().Throw<DomainRuleException>().Which.Field.Should().Be("cron");
    }

    [Fact]
    public void Routes_can_be_added_updated_and_removed()
    {
        var watch = NewWatch();
        var route = watch.AddRoute(
            NotificationChannel.Discord,
            "https://discord.com/api/webhooks/1/abc",
            NotificationTrigger.NewListing,
            true
        );

        route.Update(null, NotificationTrigger.NewListing | NotificationTrigger.Reposted, false);

        route.Triggers.Should().HaveFlag(NotificationTrigger.Reposted);
        route.IsEnabled.Should().BeFalse();
        watch.RemoveRoute(route.Id).Should().BeTrue();
        watch.NotificationRoutes.Should().BeEmpty();
    }

    [Fact]
    public void Route_without_triggers_is_rejected()
    {
        var act = () =>
            NewWatch()
                .AddRoute(
                    NotificationChannel.Email,
                    "me@example.com",
                    NotificationTrigger.None,
                    true
                );
        act.Should().Throw<DomainRuleException>().Which.Field.Should().Be("triggers");
    }
}
