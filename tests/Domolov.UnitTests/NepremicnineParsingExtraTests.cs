using Domolov.Domain.Services;
using FluentAssertions;

namespace Domolov.UnitTests;

public sealed class NepremicnineParsingExtraTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    public void TryExtractExternalId_rejects_invalid(string? url)
    {
        NepremicnineParsing.TryExtractExternalId(url!).Should().BeNull();
    }

    [Fact]
    public void TryExtractExternalId_null_for_root_path()
    {
        NepremicnineParsing.TryExtractExternalId("https://www.nepremicnine.net/").Should().BeNull();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("cf-browser-verification", true)]
    [InlineData("challenge-platform", true)]
    [InlineData("Cloudflare Attention Required", true)]
    public void LooksLikeCloudflareChallenge_variants(string? html, bool expected)
    {
        NepremicnineParsing.LooksLikeCloudflareChallenge(html!).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    [InlineData("1.200 EUR", true)]
    public void TryParsePrice_variants(string? raw, bool ok)
    {
        var success = NepremicnineParsing.TryParsePrice(raw, out var amount);
        success.Should().Be(ok);
        if (ok)
        {
            amount.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void GetNextOccurrence_returns_future_slot()
    {
        var from = DateTimeOffset.Parse("2026-08-30T12:05:00Z");
        var next = WatchSchedule.GetNextOccurrence("0 * * * *", from, TimeZoneInfo.Utc);
        next.Should().NotBeNull();
        next!.Value.Should().BeAfter(from);
    }

    [Fact]
    public void GetNextOccurrence_invalid_cron_throws()
    {
        var act = () =>
            WatchSchedule.GetNextOccurrence("not-a-cron", DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
        act.Should().Throw<Cronos.CronFormatException>();
    }
}
