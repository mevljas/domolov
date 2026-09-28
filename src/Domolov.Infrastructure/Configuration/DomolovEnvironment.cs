using System.Collections;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Domolov.Infrastructure.Configuration;

/// <summary>
/// Maps DOMOLOV_* environment variables onto the "Domolov" configuration section:
/// DOMOLOV_MAX_CONCURRENT_SCANS → Domolov:MaxConcurrentScans,
/// DOMOLOV_RETENTION_DELISTED_DAYS → Domolov:Retention:DelistedDays,
/// DOMOLOV_MATCH_AUTO_LINK_SCORE → Domolov:Match:AutoLinkScore.
/// </summary>
public static class DomolovEnvironment
{
    public const string Prefix = "DOMOLOV_";

    /// <summary>Variables consumed elsewhere (logging, the web container) rather than as options.</summary>
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
    {
        "DOMOLOV_LOG_LEVEL",
        "DOMOLOV_LOG_FILE",
        "DOMOLOV_API_UPSTREAM",
    };

    private static readonly (string EnvPrefix, string Section)[] Sections =
    [
        ("RETENTION_", "Domolov:Retention:"),
        ("MATCH_", "Domolov:Match:"),
    ];

    public static IConfigurationBuilder AddDomolovEnvironmentVariables(
        this IConfigurationBuilder builder
    ) => builder.AddInMemoryCollection(Translate(Environment.GetEnvironmentVariables()));

    public static IEnumerable<KeyValuePair<string, string?>> Translate(IDictionary environment)
    {
        foreach (DictionaryEntry entry in environment)
        {
            var name = entry.Key?.ToString();
            if (
                name is null
                || !name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                || Excluded.Contains(name)
            )
            {
                continue;
            }

            var value = entry.Value?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                // Compose passes unset optional variables as empty strings; treat them as unset.
                continue;
            }

            var key = ToKey(name[Prefix.Length..]);
            if (
                key == "Domolov:Role"
                && string.Equals(value, "web", StringComparison.OrdinalIgnoreCase)
            )
            {
                value = "Api";
            }

            yield return new KeyValuePair<string, string?>(key, value);
        }
    }

    public static string ToKey(string snake)
    {
        foreach (var (envPrefix, section) in Sections)
        {
            if (snake.StartsWith(envPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return section + Pascal(snake[envPrefix.Length..]);
            }
        }

        return "Domolov:" + Pascal(snake);
    }

    private static string Pascal(string snake)
    {
        var sb = new StringBuilder();
        foreach (var part in snake.Split('_', StringSplitOptions.RemoveEmptyEntries))
        {
            sb.Append(char.ToUpper(part[0], CultureInfo.InvariantCulture));
            sb.Append(part[1..].ToLowerInvariant());
        }

        return sb.ToString();
    }
}
