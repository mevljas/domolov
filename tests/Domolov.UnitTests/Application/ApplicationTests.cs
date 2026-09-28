using Domolov.Application.Abstractions;
using Domolov.Application.Auth;
using Domolov.Application.Notifications;
using Domolov.Application.Options;
using Domolov.Application.Retention;
using Domolov.Application.Watches;
using Domolov.Domain.Listings;
using Domolov.Domain.Notifications;
using Domolov.Domain.Watches;
using Domolov.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Domolov.UnitTests.Application;

public sealed class PasswordHashingTests
{
    [Fact]
    public void Hash_verifies_the_right_password_only()
    {
        var hash = PasswordHashing.Hash("correct horse", iterations: 1000);

        hash.Should().StartWith("pbkdf2-sha256$1000$");
        PasswordHashing.Verify("correct horse", hash).Should().BeTrue();
        PasswordHashing.Verify("wrong", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("pbkdf2-sha256$x$a$b")]
    [InlineData("pbkdf2-sha256$1000$***$***")]
    public void Malformed_hashes_never_verify(string encoded) =>
        PasswordHashing.Verify("x", encoded).Should().BeFalse();

    [Fact]
    public void Fixed_time_equality_matches_ordinal_equality()
    {
        PasswordHashing.FixedTimeEquals("abc", "abc").Should().BeTrue();
        PasswordHashing.FixedTimeEquals("abc", "abd").Should().BeFalse();
        PasswordHashing.FixedTimeEquals("abc", "abcd").Should().BeFalse();
    }
}

public sealed class SearchUrlNamesTests
{
    [Theory]
    [InlineData(
        "https://www.nepremicnine.net/oglasi-prodaja/ljubljana-mesto/stanovanje/",
        "Stanovanje · Ljubljana mesto"
    )]
    [InlineData(
        "https://www.nepremicnine.net/oglasi-prodaja/ljubljana-mesto/stanovanje/2-sobno/",
        "Stanovanje · Ljubljana mesto · 2 sobno"
    )]
    [InlineData("https://www.nepremicnine.net/", "www.nepremicnine.net")]
    public void Suggests_readable_names(string url, string expected) =>
        SearchUrlNames.Suggest(new Uri(url)).Should().Be(expected);
}

public sealed class CleanupScheduleTests
{
    [Fact]
    public void Next_cleanup_is_today_when_the_time_has_not_passed_yet()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Ljubljana");
        var now = DateTimeOffset.Parse("2026-09-01T00:30:00Z");
        CleanupSchedule
            .Next("04:00", now, tz)
            .Should()
            .Be(DateTimeOffset.Parse("2026-09-01T02:00:00Z"));
    }

    [Fact]
    public void Next_cleanup_is_tomorrow_when_the_time_has_passed()
    {
        var now = DateTimeOffset.Parse("2026-09-01T05:00:00Z");
        CleanupSchedule
            .Next("04:00", now, TimeZoneInfo.Utc)
            .Should()
            .Be(DateTimeOffset.Parse("2026-09-02T04:00:00Z"));
    }
}

public sealed class DomolovEnvironmentTests
{
    [Fact]
    public void Translates_domolov_variables_into_configuration_keys()
    {
        var env = new Dictionary<string, string>
        {
            ["DOMOLOV_MAX_CONCURRENT_SCANS"] = "2",
            ["DOMOLOV_RETENTION_DELISTED_DAYS"] = "10",
            ["DOMOLOV_MATCH_AUTO_LINK_SCORE"] = "0.9",
            ["DOMOLOV_ROLE"] = "web",
            ["DOMOLOV_LOG_LEVEL"] = "Debug",
            ["DOMOLOV_SMTP_PORT"] = "",
            ["PATH"] = "/usr/bin",
        };

        var translated = DomolovEnvironment
            .Translate(env)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        translated.Should().Contain("Domolov:MaxConcurrentScans", "2");
        translated.Should().Contain("Domolov:Retention:DelistedDays", "10");
        translated.Should().Contain("Domolov:Match:AutoLinkScore", "0.9");
        translated.Should().Contain("Domolov:Role", "Api");
        translated.Should().NotContainKey("Domolov:LogLevel");
        translated.Should().NotContainKey("Domolov:SmtpPort");
        translated.Should().HaveCount(4);
    }

    [Theory]
    [InlineData("VAPID_PUBLIC_KEY", "Domolov:VapidPublicKey")]
    [InlineData("ADMIN_PASSWORD_HASH", "Domolov:AdminPasswordHash")]
    [InlineData("RETENTION_SCAN_RUN_KEEP_PER_WATCH", "Domolov:Retention:ScanRunKeepPerWatch")]
    public void Builds_pascal_case_keys(string snake, string key) =>
        DomolovEnvironment.ToKey(snake).Should().Be(key);
}

public sealed class NotificationDispatcherTests
{
    private static NotificationDispatcher Dispatcher(
        INotifier notifier,
        string? publicUrl = null
    ) =>
        new(
            [notifier],
            Microsoft.Extensions.Options.Options.Create(
                new DomolovOptions { PublicUrl = publicUrl }
            ),
            new FakeTimeProvider(TestData.Now.AddDays(21)),
            NullLogger<NotificationDispatcher>.Instance
        );

    private static NotificationRoute Route(NotificationTrigger triggers)
    {
        var watch = new Watch(
            "w",
            "nepremicnine",
            "https://www.nepremicnine.net/x/",
            "0 * * * *",
            TestData.Now
        );
        return watch.AddRoute(
            NotificationChannel.Discord,
            "https://discord.com/api/webhooks/1/a",
            triggers,
            true
        );
    }

    [Fact]
    public async Task Sends_matching_events_and_skips_dismissed_homes()
    {
        var notifier = Substitute.For<INotifier>();
        notifier.Channel.Returns(NotificationChannel.Discord);
        var (home, listing) = TestData.HomeWith(TestData.Card("1"));
        var (dismissed, other) = TestData.HomeWith(TestData.Card("2"));
        dismissed.Dismiss(TestData.Now);
        var route = Route(NotificationTrigger.NewListing);

        var errors = await Dispatcher(notifier)
            .DispatchAsync(
                [route],
                [
                    new HomeEvent(ListingChangeKind.New, home, listing, null),
                    new HomeEvent(ListingChangeKind.New, dismissed, other, null),
                ],
                CancellationToken.None
            );

        errors.Should().BeEmpty();
        await notifier
            .Received(1)
            .SendAsync(
                Arg.Is<NotificationMessage>(m => m.HomeId == home.Id),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Notifier_failures_are_reported_not_thrown()
    {
        var notifier = Substitute.For<INotifier>();
        notifier.Channel.Returns(NotificationChannel.Discord);
        notifier
            .SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HttpRequestException("404 Unknown Webhook")));
        var (home, listing) = TestData.HomeWith(TestData.Card());

        var errors = await Dispatcher(notifier)
            .DispatchAsync(
                [Route(NotificationTrigger.NewListing)],
                [new HomeEvent(ListingChangeKind.New, home, listing, null)],
                CancellationToken.None
            );

        errors.Should().ContainSingle().Which.Should().Contain("Unknown Webhook");
    }

    [Fact]
    public void Repost_message_compares_with_the_previous_ad()
    {
        var notifier = Substitute.For<INotifier>();
        notifier.Channel.Returns(NotificationChannel.Discord);
        var (home, listing) = TestData.HomeWith(TestData.Card(price: 450_000m));

        var message = Dispatcher(notifier, "https://domolov.example.com/")
            .Compose(
                Route(NotificationTrigger.Reposted),
                new HomeEvent(ListingChangeKind.Reposted, home, listing, 470_000m, TestData.Now)
            );

        message.Body.Should().Be("Reposted after 3 weeks, 20.000 € cheaper (was 470.000 €)");
        message.AppUrl.Should().Be($"https://domolov.example.com/homes/{home.Id}");
        message.AppPath.Should().Be($"/homes/{home.Id}");
    }

    [Fact]
    public void Price_drop_message_includes_the_percentage()
    {
        var notifier = Substitute.For<INotifier>();
        var (home, listing) = TestData.HomeWith(TestData.Card(price: 300_000m));
        listing.RecordPrice(285_000m, "EUR", TestData.Now);
        home.Refresh([listing], TestData.Now);

        var message = Dispatcher(notifier)
            .Compose(
                Route(NotificationTrigger.PriceDecreased),
                new HomeEvent(ListingChangeKind.PriceDecreased, home, listing, 300_000m)
            );

        message.Body.Should().Be("Price dropped from 300.000 € to 285.000 € (5 %)");
    }
}
