using System.Globalization;
using System.Text.RegularExpressions;
using Cronos;

namespace Domolov.Domain.Services;

/// <summary>Helpers for Nepremicnine URLs and Cloudflare HTML sniffing.</summary>
public static partial class NepremicnineParsing
{
    public const string ProviderId = "nepremicnine";

    [GeneratedRegex(@"nepremicnine\.net", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HostRegex();

    public static bool IsNepremicnineHost(Uri url) => HostRegex().IsMatch(url.Host);

    /// <summary>Listing id is typically the last non-empty path segment.</summary>
    public static string? TryExtractExternalId(string listingUrl)
    {
        if (!Uri.TryCreate(listingUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 1)
        {
            return null;
        }

        return segments[^1];
    }

    public static Uri BuildPageUrl(Uri searchUrl, int pageIndex)
    {
        if (pageIndex <= 1)
        {
            return searchUrl;
        }

        var path = searchUrl.AbsolutePath.TrimEnd('/') + $"/{pageIndex}/";
        var builder = new UriBuilder(searchUrl) { Path = path };
        return builder.Uri;
    }

    public static bool LooksLikeCloudflareChallenge(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        return html.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase)
            || html.Contains("challenge-platform", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Just a moment...", StringComparison.OrdinalIgnoreCase)
            || (
                html.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase)
                && html.Contains("Attention Required", StringComparison.OrdinalIgnoreCase)
            );
    }

    public static bool TryParsePrice(string? raw, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw.Replace("€", "", StringComparison.Ordinal)
            .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace(".", "", StringComparison.Ordinal)
            .Replace(',', '.')
            .Trim();

        return decimal.TryParse(
            cleaned,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount
        );
    }
}

/// <summary>Cron evaluation in a configured IANA timezone.</summary>
public static class WatchSchedule
{
    public static DateTimeOffset? GetNextOccurrence(
        string cronExpression,
        DateTimeOffset fromUtc,
        TimeZoneInfo timeZone
    )
    {
        var cron = CronExpression.Parse(cronExpression);
        var from = DateTime.SpecifyKind(fromUtc.UtcDateTime, DateTimeKind.Utc);
        var next = cron.GetNextOccurrence(from, timeZone);
        if (next is null)
        {
            return null;
        }

        return new DateTimeOffset(next.Value, TimeSpan.Zero);
    }

    public static bool IsDue(
        string cronExpression,
        DateTimeOffset? lastScannedAt,
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone
    )
    {
        var from = lastScannedAt ?? nowUtc.AddYears(-1);
        var next = GetNextOccurrence(cronExpression, from, timeZone);
        return next is not null && next <= nowUtc.ToUniversalTime();
    }
}
