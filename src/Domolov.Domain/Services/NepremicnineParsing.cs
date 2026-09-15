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
            .Trim();

        var hasDot = cleaned.Contains('.', StringComparison.Ordinal);
        var hasComma = cleaned.Contains(',', StringComparison.Ordinal);

        if (hasDot && hasComma)
        {
            // Slovenian: 280.000,00
            cleaned = cleaned.Replace(".", "", StringComparison.Ordinal).Replace(',', '.');
        }
        else if (hasComma)
        {
            cleaned = cleaned.Replace(',', '.');
        }
        else if (hasDot)
        {
            var lastDot = cleaned.LastIndexOf('.');
            var digitsAfter = cleaned.Length - lastDot - 1;
            if (digitsAfter is 1 or 2)
            {
                // Schema / invariant decimal: 280000.00
            }
            else
            {
                // Thousand separators only: 280.000
                cleaned = cleaned.Replace(".", "", StringComparison.Ordinal);
            }
        }

        return decimal.TryParse(
            cleaned,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount
        );
    }

    /// <summary>Parsed attribute fragments from a nepremicnine listing card.</summary>
    public sealed record CardAttributes(
        string? PropertyType,
        string? Rooms,
        string? SizeText,
        string? YearText,
        string? FloorText,
        string? LandSizeText
    );

    /// <summary>
    /// Extracts type/rooms/size/year/floor/land from the category line and/or <c>.kratek</c> blurb.
    /// </summary>
    public static CardAttributes ParseCardAttributes(string? categoryLine, string? description)
    {
        string? propertyType = null;
        string? rooms = null;
        string? sizeText = null;
        string? yearText = null;
        string? floorText = null;
        string? landSizeText = null;

        if (!string.IsNullOrWhiteSpace(categoryLine))
        {
            var cat = categoryLine.Trim();
            var dealMatch = DealTypeRegex().Match(cat);
            if (dealMatch.Success)
            {
                propertyType = NullIfEmpty(dealMatch.Groups["type"].Value.Trim());
                rooms = NullIfEmpty(dealMatch.Groups["rooms"].Value.Trim());
            }
            else
            {
                propertyType ??= NullIfEmpty(PropertyTypeRegex().Match(cat).Groups["type"].Value);
                rooms ??= NullIfEmpty(RoomsRegex().Match(cat).Groups["rooms"].Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            var text = description.Trim();
            landSizeText = FormatM2(LandSizeRegex().Match(text).Groups["size"].Value);
            sizeText = FormatM2(LivingSizeRegex().Match(text).Groups["size"].Value);
            yearText = NullIfEmpty(YearBuiltRegex().Match(text).Groups["year"].Value);
            if (yearText is null)
            {
                yearText = NullIfEmpty(BareYearRegex().Match(text).Groups["year"].Value);
            }

            floorText = NullIfEmpty(FloorRegex().Match(text).Groups["floor"].Value);
            rooms ??= NullIfEmpty(RoomsRegex().Match(text).Groups["rooms"].Value);
            propertyType ??= NullIfEmpty(PropertyTypeRegex().Match(text).Groups["type"].Value);
        }

        return new CardAttributes(propertyType, rooms, sizeText, yearText, floorText, landSizeText);
    }

    private static string? FormatM2(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = raw.Trim().Replace(",", ".", StringComparison.Ordinal);
        if (
            decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value
            )
        )
        {
            var whole = decimal.Truncate(value);
            if (value == whole)
            {
                return $"{whole.ToString(CultureInfo.InvariantCulture)} m2";
            }

            return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} m2";
        }

        return $"{raw.Trim()} m2";
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(
        @"^(?:Prodaja|Oddaja)\s*:\s*(?<type>[^,]+)(?:,\s*(?<rooms>.+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex DealTypeRegex();

    [GeneratedRegex(
        @"(?<type>Stanovanje|Hiša|Hisa|Poslovni\s+prostor|Zemljišče|Zemljisce|Garaza|Garaža|Sob[ae]|Vikend)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex PropertyTypeRegex();

    [GeneratedRegex(
        @"(?<rooms>\d+(?:[.,]\d+)?-sobn[oa]|garsonjera)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex RoomsRegex();

    [GeneratedRegex(
        @"(?<size>\d+(?:[.,]\d+)?)\s*m2\s*zemljiš",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex LandSizeRegex();

    [GeneratedRegex(
        @"(?<size>\d+(?:[.,]\d+)?)\s*m2(?!\s*zemljiš)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex LivingSizeRegex();

    [GeneratedRegex(
        @"zgrajen[aio]?\s+l\.\s*(?<year>\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex YearBuiltRegex();

    [GeneratedRegex(@"(?<!\d)(?<year>19\d{2}|20\d{2})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex BareYearRegex();

    [GeneratedRegex(
        @"(?<floor>PK/\d+|\d+\s*/\s*\d+\s*nad\.?|\d+\.\s*nad\.?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex FloorRegex();
}

/// <summary>Crawl page-loop stop policy for search result pagination.</summary>
public static class CrawlPagination
{
    public const int DefaultMaxPages = 50;

    public static bool ShouldFetchPage(
        int pageIndex,
        int consecutiveEmptyPages,
        int maxPages = DefaultMaxPages
    ) => consecutiveEmptyPages < 1 && pageIndex <= maxPages;

    public static int NextEmptyStreak(int foundOnPage, int consecutiveEmptyPages) =>
        foundOnPage == 0 ? consecutiveEmptyPages + 1 : 0;
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
