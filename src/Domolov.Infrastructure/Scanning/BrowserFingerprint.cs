using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Domolov.Infrastructure.Scanning;

/// <summary>
/// The "device" Domolov presents to sites. Generated once and persisted next to the browser
/// profile so every scan looks like the same desktop.
/// </summary>
public sealed record BrowserFingerprint(
    int ScreenWidth,
    int ScreenHeight,
    int ViewportWidth,
    int ViewportHeight,
    double DeviceScaleFactor,
    DateTimeOffset CreatedAt
)
{
    public const string AcceptLanguage = "sl-SI,sl;q=0.9,en;q=0.8";
    public const string Locale = "sl-SI";

    /// <summary>Common desktop resolutions (browser window maximised).</summary>
    private static readonly (int Width, int Height)[] CommonScreens =
    [
        (1920, 1080),
        (1536, 864),
        (1440, 900),
        (1366, 768),
        (1600, 900),
        (1680, 1050),
    ];

    /// <summary>Vertical space taken by tabs, address bar and bookmarks bar.</summary>
    private const int BrowserChromeHeight = 111;

    public static BrowserFingerprint Generate(Random random, DateTimeOffset now)
    {
        var (w, h) = CommonScreens[random.Next(CommonScreens.Length)];
        return new BrowserFingerprint(w, h, w, h - BrowserChromeHeight, 1, now);
    }
}

/// <summary>Loads or creates the persisted fingerprint.</summary>
public static class BrowserFingerprintStore
{
    public const string FileName = "domolov-fingerprint.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static BrowserFingerprint LoadOrCreate(
        string directory,
        Random random,
        DateTimeOffset now
    )
    {
        var path = Path.Combine(directory, FileName);
        try
        {
            if (File.Exists(path))
            {
                var existing = JsonSerializer.Deserialize<BrowserFingerprint>(
                    File.ReadAllText(path),
                    Json
                );
                if (existing is { ScreenWidth: > 0, ViewportHeight: > 0 })
                {
                    return existing;
                }
            }
        }
        catch (JsonException)
        {
            // Corrupt file: regenerate below.
        }

        var fingerprint = BrowserFingerprint.Generate(random, now);
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(fingerprint, Json));
        return fingerprint;
    }
}

/// <summary>Builds a Chrome-on-Linux UA and matching client hints from the real browser version.</summary>
public static partial class UserAgentBuilder
{
    /// <summary>Extracts "140.0.7339.16" from "Chrome/140.0.7339.16" or "HeadlessChrome/140...".</summary>
    public static string? ParseVersion(string product)
    {
        var match = VersionRegex().Match(product);
        return match.Success ? match.Groups["v"].Value : null;
    }

    public static string Major(string fullVersion) => fullVersion.Split('.')[0];

    /// <summary>Reduced UA string as Chrome sends it (minor versions zeroed).</summary>
    public static string UserAgent(string fullVersion) =>
        $"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{Major(fullVersion)}.0.0.0 Safari/537.36";

    /// <summary>Arguments for CDP <c>Emulation.setUserAgentOverride</c>, including client hints.</summary>
    public static Dictionary<string, object> OverrideArguments(string fullVersion)
    {
        var major = Major(fullVersion);
        var grease = GreaseBrand(int.Parse(major, CultureInfo.InvariantCulture));
        return new Dictionary<string, object>
        {
            ["userAgent"] = UserAgent(fullVersion),
            ["acceptLanguage"] = BrowserFingerprint.AcceptLanguage,
            ["platform"] = "Linux x86_64",
            ["userAgentMetadata"] = new Dictionary<string, object>
            {
                ["brands"] = new[]
                {
                    Brand("Chromium", major),
                    Brand(grease, "24"),
                    Brand("Google Chrome", major),
                },
                ["fullVersionList"] = new[]
                {
                    Brand("Chromium", fullVersion),
                    Brand(grease, "24.0.0.0"),
                    Brand("Google Chrome", fullVersion),
                },
                ["fullVersion"] = fullVersion,
                ["platform"] = "Linux",
                ["platformVersion"] = "6.8.0",
                ["architecture"] = "x86",
                ["model"] = "",
                ["mobile"] = false,
                ["bitness"] = "64",
                ["wow64"] = false,
            },
        };
    }

    /// <summary>Chrome rotates its GREASE brand by major version; pick deterministically.</summary>
    private static string GreaseBrand(int major)
    {
        string[] chars = [" ", "(", ":", "-", ".", "/", ")", ";", "=", "?", "_"];
        string[] names = ["Not", "A", "Brand"];
        var a = chars[major % chars.Length];
        var b = chars[(major + 1) % chars.Length];
        return $"{names[0]}{a}{names[1]}{b}{names[2]}";
    }

    private static Dictionary<string, string> Brand(string brand, string version) =>
        new() { ["brand"] = brand, ["version"] = version };

    [GeneratedRegex(@"Chrom(e|ium)/(?<v>\d+\.\d+\.\d+\.\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();
}
