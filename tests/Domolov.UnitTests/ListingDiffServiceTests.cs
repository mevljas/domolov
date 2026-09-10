using Domolov.Domain.Enums;
using Domolov.Domain.Services;
using FluentAssertions;

namespace Domolov.UnitTests;

public sealed class ListingDiffServiceTests
{
    [Theory]
    [InlineData(null, 100.0, ListingChangeKind.New)]
    [InlineData(100.0, 100.0, ListingChangeKind.Unchanged)]
    [InlineData(100.0, 90.0, ListingChangeKind.PriceDecreased)]
    [InlineData(100.0, 110.0, ListingChangeKind.PriceIncreased)]
    [InlineData(100.0, null, ListingChangeKind.Unchanged)]
    public void Classify_returns_expected_kind(
        double? previous,
        double? current,
        ListingChangeKind expected
    )
    {
        decimal? p = previous is null ? null : (decimal)previous.Value;
        decimal? c = current is null ? null : (decimal)current.Value;
        ListingDiffService.Classify(p, c).Should().Be(expected);
    }

    [Theory]
    [InlineData(NotificationTrigger.NewListing, ListingChangeKind.New, true)]
    [InlineData(NotificationTrigger.NewListing, ListingChangeKind.PriceDecreased, false)]
    [InlineData(NotificationTrigger.PriceDecreased, ListingChangeKind.PriceDecreased, true)]
    [InlineData(NotificationTrigger.AnyPriceChange, ListingChangeKind.PriceIncreased, true)]
    [InlineData(NotificationTrigger.AnyPriceChange, ListingChangeKind.New, false)]
    [InlineData(NotificationTrigger.None, ListingChangeKind.New, false)]
    public void MatchesTrigger_respects_flags(
        NotificationTrigger triggers,
        ListingChangeKind kind,
        bool expected
    )
    {
        ListingDiffService.MatchesTrigger(triggers, kind).Should().Be(expected);
    }
}

public sealed class NepremicnineParsingTests
{
    [Fact]
    public void TryExtractExternalId_reads_second_to_last_segment()
    {
        var id = NepremicnineParsing.TryExtractExternalId(
            "https://www.nepremicnine.net/oglasi-prodaja/ljubljana/stanovanje/12345/"
        );
        id.Should().Be("12345");
    }

    [Fact]
    public void BuildPageUrl_appends_page_index()
    {
        var baseUrl = new Uri("https://www.nepremicnine.net/oglasi-prodaja/ljubljana/stanovanje/");
        NepremicnineParsing.BuildPageUrl(baseUrl, 1).Should().Be(baseUrl);
        NepremicnineParsing
            .BuildPageUrl(baseUrl, 2)
            .ToString()
            .Should()
            .Be("https://www.nepremicnine.net/oglasi-prodaja/ljubljana/stanovanje/2/");
    }

    [Fact]
    public void LooksLikeCloudflareChallenge_detects_interstitial()
    {
        NepremicnineParsing
            .LooksLikeCloudflareChallenge("<html>Just a moment...</html>")
            .Should()
            .BeTrue();
        NepremicnineParsing
            .LooksLikeCloudflareChallenge("<html>ok listings</html>")
            .Should()
            .BeFalse();
    }

    [Fact]
    public void TryParsePrice_handles_european_formats()
    {
        NepremicnineParsing.TryParsePrice("250.000,00 €", out var amount).Should().BeTrue();
        amount.Should().Be(250000.00m);
    }

    [Fact]
    public void IsNepremicnineHost_matches()
    {
        NepremicnineParsing
            .IsNepremicnineHost(new Uri("https://www.nepremicnine.net/oglasi-prodaja/"))
            .Should()
            .BeTrue();
        NepremicnineParsing.IsNepremicnineHost(new Uri("https://example.com")).Should().BeFalse();
    }
}

public sealed class WatchScheduleTests
{
    [Fact]
    public void IsDue_when_never_scanned_and_cron_hourly()
    {
        var tz = TimeZoneInfo.Utc;
        var now = DateTimeOffset.Parse("2026-08-30T12:05:00Z");
        WatchSchedule.IsDue("0 * * * *", null, now, tz).Should().BeTrue();
    }

    [Fact]
    public void IsDue_false_immediately_after_scan()
    {
        var tz = TimeZoneInfo.Utc;
        var now = DateTimeOffset.Parse("2026-08-30T12:05:00Z");
        var last = DateTimeOffset.Parse("2026-08-30T12:00:00Z");
        WatchSchedule.IsDue("0 * * * *", last, now, tz).Should().BeFalse();
    }
}
