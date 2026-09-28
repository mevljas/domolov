using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace Domolov.Api.Endpoints;

/// <summary>Weak ETags over PostgreSQL xmin versions.</summary>
public static class EntityTags
{
    public static string For(uint version) =>
        $"W/\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    public static void Set(HttpResponse response, uint version) =>
        response.Headers.ETag = For(version);

    /// <summary>Reads the version from If-Match; null when absent or "*".</summary>
    public static uint? IfMatch(HttpRequest request)
    {
        var header = request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(header) || header.Trim() == "*")
        {
            return null;
        }

        if (!EntityTagHeaderValue.TryParse(header.Split(',')[0].Trim(), out var tag))
        {
            throw new BadHttpRequestException("If-Match must be an ETag returned by the API.");
        }

        var raw = tag.Tag.ToString().Trim('"');
        return uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : throw new BadHttpRequestException("If-Match must be an ETag returned by the API.");
    }
}
