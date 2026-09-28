using System.Globalization;

namespace Domolov.Application.Watches;

/// <summary>Suggests a human-friendly Watch name from a search URL path.</summary>
public static class SearchUrlNames
{
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase)
    {
        "oglasi-prodaja",
        "oglasi-oddaja",
        "oglasi-nakup",
        "oglasi-najem",
        "en",
        "sl",
    };

    /// <summary>
    /// "/oglasi-prodaja/ljubljana-mesto/stanovanje/2-sobno/" becomes
    /// "Stanovanje · Ljubljana mesto · 2 sobno" (deal type dropped, most specific first).
    /// </summary>
    public static string Suggest(Uri url)
    {
        var parts = url
            .AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !Noise.Contains(p) && !int.TryParse(p, out _))
            .Select(Humanize)
            .Where(p => p.Length > 0)
            .ToList();
        if (parts.Count == 0)
        {
            return url.Host;
        }

        if (parts.Count >= 2)
        {
            (parts[0], parts[1]) = (parts[1], parts[0]);
        }

        var name = string.Join(" · ", parts.Take(4));
        return name.Length <= 200 ? name : name[..200];
    }

    private static string Humanize(string segment)
    {
        var text = Uri.UnescapeDataString(segment).Replace('-', ' ').Replace('_', ' ').Trim();
        return text.Length == 0
            ? text
            : char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
    }
}
