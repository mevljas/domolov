using System.Globalization;
using System.Text.RegularExpressions;

namespace Domolov.Domain.Listings;

/// <summary>Numeric attributes parsed from scraped free text.</summary>
public sealed record ParsedAttributes(
    decimal? SizeM2,
    decimal? LandSizeM2,
    decimal? RoomCount,
    int? YearBuilt
);

/// <summary>Parses Size, LandSize, Rooms and YearBuilt from the provider's display text.</summary>
public static partial class ListingAttributeParser
{
    public static ParsedAttributes Parse(
        string? sizeText,
        string? landSizeText,
        string? roomsText,
        string? yearText
    ) =>
        new(
            ParseArea(sizeText),
            ParseArea(landSizeText),
            ParseRooms(roomsText),
            ParseYear(yearText)
        );

    /// <summary>"54,2 m2", "54.2 m²", "1.250 m2" become 54.2, 54.2 and 1250.</summary>
    public static decimal? ParseArea(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = NumberRegex().Match(text);
        return match.Success ? ParseNumber(match.Value) : null;
    }

    /// <summary>"2,5-sobno" becomes 2.5, "3-sobno" 3, "garsonjera" 1.</summary>
    public static decimal? ParseRooms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (text.Contains("garsonjer", StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var match = RoomsRegex().Match(text);
        return match.Success ? ParseNumber(match.Groups["n"].Value) : null;
    }

    public static int? ParseYear(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = YearRegex().Match(text);
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : null;
    }

    private static decimal? ParseNumber(string raw)
    {
        var value = raw.Trim();
        var hasDot = value.Contains('.', StringComparison.Ordinal);
        var hasComma = value.Contains(',', StringComparison.Ordinal);
        if (hasDot && hasComma)
        {
            value = value.Replace(".", "", StringComparison.Ordinal).Replace(',', '.');
        }
        else if (hasComma)
        {
            value = value.Replace(',', '.');
        }
        else if (hasDot && value.Length - value.LastIndexOf('.') - 1 == 3)
        {
            // "1.250" is a thousands separator, "54.2" is a decimal point.
            value = value.Replace(".", "", StringComparison.Ordinal);
        }

        return decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed
        )
            ? parsed
            : null;
    }

    [GeneratedRegex(@"\d+(?:[.,]\d+)*", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(
        @"(?<n>\d+(?:[.,]\d+)?)\s*-?\s*sob",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex RoomsRegex();

    [GeneratedRegex(@"(?<!\d)(1[89]\d{2}|20\d{2})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex YearRegex();
}
