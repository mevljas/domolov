using Domolov.Domain.Services;
using FluentAssertions;

namespace Domolov.UnitTests;

public sealed class WatchCronScheduleTests
{
    [Theory]
    [InlineData(WatchCronMode.EveryHour, WatchCronSchedule.EveryHourCron)]
    [InlineData(WatchCronMode.Every6Hours, WatchCronSchedule.Every6HoursCron)]
    [InlineData(WatchCronMode.Every12Hours, WatchCronSchedule.Every12HoursCron)]
    public void Interval_modes_round_trip(WatchCronMode mode, string cron)
    {
        var composed = WatchCronSchedule.ToCron(new WatchCronModel { Mode = mode });
        composed.Should().Be(cron);

        var parsed = WatchCronSchedule.Parse(cron);
        parsed.Mode.Should().Be(mode);
    }

    [Fact]
    public void Weekly_weekdays_at_time_round_trips()
    {
        var model = new WatchCronModel
        {
            Mode = WatchCronMode.Weekly,
            Time = new TimeOnly(9, 30),
            Days =
            [
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
            ],
        };

        var cron = WatchCronSchedule.ToCron(model);
        cron.Should().Be("30 9 * * 1-5");

        var parsed = WatchCronSchedule.Parse(cron);
        parsed.Mode.Should().Be(WatchCronMode.Weekly);
        parsed.Time.Should().Be(new TimeOnly(9, 30));
        parsed.Days.Should().Equal(model.Days);
    }

    [Fact]
    public void Weekly_all_days_uses_star_day_field()
    {
        var model = new WatchCronModel
        {
            Mode = WatchCronMode.Weekly,
            Time = new TimeOnly(8, 0),
            Days = WatchCronModel.AllDays,
        };

        var cron = WatchCronSchedule.ToCron(model);
        cron.Should().Be("0 8 * * *");

        var parsed = WatchCronSchedule.Parse(cron);
        parsed.Mode.Should().Be(WatchCronMode.Weekly);
        parsed.Time.Should().Be(new TimeOnly(8, 0));
        parsed.Days.Should().HaveCount(7);
    }

    [Fact]
    public void Weekly_noncontiguous_days_list()
    {
        var model = new WatchCronModel
        {
            Mode = WatchCronMode.Weekly,
            Time = new TimeOnly(7, 15),
            Days = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
        };

        var cron = WatchCronSchedule.ToCron(model);
        cron.Should().Be("15 7 * * 1,3,5");

        var parsed = WatchCronSchedule.Parse(cron);
        parsed.Mode.Should().Be(WatchCronMode.Weekly);
        parsed.Days.Should().Equal(model.Days);
    }

    [Fact]
    public void Unparseable_cron_is_custom()
    {
        var parsed = WatchCronSchedule.Parse("15 2 1 * *");
        parsed.Mode.Should().Be(WatchCronMode.Custom);
        parsed.CustomCron.Should().Be("15 2 1 * *");
        WatchCronSchedule.ToCron(parsed).Should().Be("15 2 1 * *");
    }

    [Fact]
    public void Empty_cron_defaults_to_every_6_hours()
    {
        WatchCronSchedule.Parse("").Mode.Should().Be(WatchCronMode.Every6Hours);
        WatchCronSchedule.Parse(null).Mode.Should().Be(WatchCronMode.Every6Hours);
    }

    [Fact]
    public void TryValidate_accepts_known_expressions()
    {
        WatchCronSchedule.TryValidate("0 * * * *", out var error).Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_rejects_invalid_expressions()
    {
        WatchCronSchedule.TryValidate("not-a-cron", out var error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Sunday_as_7_normalizes_on_parse()
    {
        var parsed = WatchCronSchedule.Parse("0 10 * * 7");
        parsed.Mode.Should().Be(WatchCronMode.Weekly);
        parsed.Days.Should().Equal(DayOfWeek.Sunday);
    }
}
