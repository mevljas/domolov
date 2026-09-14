using Cronos;

namespace Domolov.Domain.Services;

/// <summary>Supported guided schedule modes that map to <c>Watch.Cron</c>.</summary>
public enum WatchCronMode
{
    EveryHour,
    Every6Hours,
    Every12Hours,
    Weekly,
    Custom,
}

/// <summary>UI-friendly schedule model backed by a 5-field cron expression.</summary>
public sealed class WatchCronModel
{
    public WatchCronMode Mode { get; init; } = WatchCronMode.EveryHour;

    /// <summary>Local time-of-day for <see cref="WatchCronMode.Weekly"/>.</summary>
    public TimeOnly Time { get; init; } = new(9, 0);

    /// <summary>Selected days for <see cref="WatchCronMode.Weekly"/> (Cronos / .NET: Sunday = 0).</summary>
    public IReadOnlyList<DayOfWeek> Days { get; init; } = AllDays;

    /// <summary>Raw cron when <see cref="Mode"/> is <see cref="WatchCronMode.Custom"/>.</summary>
    public string? CustomCron { get; init; }

    public static IReadOnlyList<DayOfWeek> AllDays { get; } =
        [
            DayOfWeek.Sunday,
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday,
            DayOfWeek.Saturday,
        ];
}

/// <summary>Compose and reverse-parse guided Watch cron schedules.</summary>
public static class WatchCronSchedule
{
    public const string EveryHourCron = "0 * * * *";
    public const string Every6HoursCron = "0 */6 * * *";
    public const string Every12HoursCron = "0 */12 * * *";

    public static string ToCron(WatchCronModel model) =>
        model.Mode switch
        {
            WatchCronMode.EveryHour => EveryHourCron,
            WatchCronMode.Every6Hours => Every6HoursCron,
            WatchCronMode.Every12Hours => Every12HoursCron,
            WatchCronMode.Weekly => ComposeWeekly(model.Time, model.Days),
            WatchCronMode.Custom => string.IsNullOrWhiteSpace(model.CustomCron)
                ? EveryHourCron
                : model.CustomCron.Trim(),
            _ => EveryHourCron,
        };

    public static WatchCronModel Parse(string? cron)
    {
        var trimmed = string.IsNullOrWhiteSpace(cron) ? EveryHourCron : cron.Trim();

        if (trimmed == EveryHourCron)
        {
            return new WatchCronModel { Mode = WatchCronMode.EveryHour };
        }

        if (trimmed == Every6HoursCron)
        {
            return new WatchCronModel { Mode = WatchCronMode.Every6Hours };
        }

        if (trimmed == Every12HoursCron)
        {
            return new WatchCronModel { Mode = WatchCronMode.Every12Hours };
        }

        if (TryParseWeekly(trimmed, out var time, out var days))
        {
            return new WatchCronModel
            {
                Mode = WatchCronMode.Weekly,
                Time = time,
                Days = days,
            };
        }

        return new WatchCronModel { Mode = WatchCronMode.Custom, CustomCron = trimmed };
    }

    public static bool TryValidate(string cron, out string? error)
    {
        try
        {
            CronExpression.Parse(cron);
            error = null;
            return true;
        }
        catch (CronFormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string ComposeWeekly(TimeOnly time, IReadOnlyList<DayOfWeek> days)
    {
        var selected = (days is { Count: > 0 } ? days : AllDaysSnapshot())
            .Distinct()
            .OrderBy(d => (int)d)
            .ToList();
        if (selected.Count == 0)
        {
            selected = AllDaysSnapshot();
        }

        var dayField =
            selected.Count == 7 ? "*" : CompressDayField(selected.Select(d => (int)d).ToList());

        return $"{time.Minute} {time.Hour} * * {dayField}";
    }

    private static bool TryParseWeekly(
        string cron,
        out TimeOnly time,
        out IReadOnlyList<DayOfWeek> days
    )
    {
        time = default;
        days = Array.Empty<DayOfWeek>();
        var parts = cron.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        if (parts.Length != 5)
        {
            return false;
        }

        if (parts[2] != "*" || parts[3] != "*")
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var minute) || minute is < 0 or > 59)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var hour) || hour is < 0 or > 23)
        {
            return false;
        }

        // Interval-style hour fields are not weekly.
        if (
            parts[1].Contains('*', StringComparison.Ordinal)
            || parts[0].Contains('*', StringComparison.Ordinal)
        )
        {
            return false;
        }

        if (parts[4] == "*")
        {
            time = new TimeOnly(hour, minute);
            days = WatchCronModel.AllDays;
            return true;
        }

        if (!TryExpandDayField(parts[4], out var dayNums))
        {
            return false;
        }

        time = new TimeOnly(hour, minute);
        days = dayNums.Select(n => (DayOfWeek)n).Distinct().OrderBy(d => (int)d).ToList();
        return days.Count > 0;
    }

    private static string CompressDayField(IReadOnlyList<int> days)
    {
        if (days.Count == 1)
        {
            return days[0].ToString();
        }

        var ranges = new List<string>();
        var start = days[0];
        var prev = days[0];
        for (var i = 1; i < days.Count; i++)
        {
            if (days[i] == prev + 1)
            {
                prev = days[i];
                continue;
            }

            ranges.Add(start == prev ? start.ToString() : $"{start}-{prev}");
            start = prev = days[i];
        }

        ranges.Add(start == prev ? start.ToString() : $"{start}-{prev}");
        return string.Join(',', ranges);
    }

    private static bool TryExpandDayField(string field, out List<int> days)
    {
        days = [];
        foreach (
            var token in field.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            if (token.Contains('-', StringComparison.Ordinal))
            {
                var ends = token.Split('-', StringSplitOptions.TrimEntries);
                if (
                    ends.Length != 2
                    || !int.TryParse(ends[0], out var from)
                    || !int.TryParse(ends[1], out var to)
                )
                {
                    return false;
                }

                // Cronos allows 0-7 with 7 = Sunday.
                from = NormalizeDow(from);
                to = NormalizeDow(to);
                if (from < 0 || to < 0 || from > to)
                {
                    return false;
                }

                for (var d = from; d <= to; d++)
                {
                    days.Add(d);
                }
            }
            else
            {
                if (!int.TryParse(token, out var day))
                {
                    return false;
                }

                day = NormalizeDow(day);
                if (day < 0)
                {
                    return false;
                }

                days.Add(day);
            }
        }

        days = days.Distinct().OrderBy(d => d).ToList();
        return days.Count > 0;
    }

    private static int NormalizeDow(int day) =>
        day switch
        {
            7 => 0,
            >= 0 and <= 6 => day,
            _ => -1,
        };

    private static List<DayOfWeek> AllDaysSnapshot() => WatchCronModel.AllDays.ToList();
}
