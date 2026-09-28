using System.Reflection;
using Domolov.Application.Options;
using Microsoft.Extensions.Options;

namespace Domolov.Application.Settings;

/// <summary>Retention settings shown in the UI.</summary>
public sealed record RetentionSettingsResponse(
    int DelistedDays,
    int OrphanListingDays,
    int ScanRunDays,
    int ScanRunKeepPerWatch,
    int ArtifactDays,
    int SessionDays,
    string DailyAt
);

/// <summary>Matching settings shown in the UI.</summary>
public sealed record MatchSettingsResponse(double AutoLinkScore, double PossibleScore);

/// <summary>Read-only server configuration and capability flags.</summary>
public sealed record SettingsResponse(
    string Version,
    string TimeZone,
    DomolovRole Role,
    int MaxConcurrentScans,
    int ScanCooldownMs,
    int CloudflareChallengeWaitMs,
    bool BrowserHeadless,
    bool FakeProvider,
    bool TelegramConfigured,
    bool SmtpConfigured,
    bool VapidConfigured,
    string? VapidPublicKey,
    string? PublicUrl,
    RetentionSettingsResponse Retention,
    MatchSettingsResponse Match
);

/// <summary>Projects options into the settings view.</summary>
public sealed class GetSettingsHandler(
    IOptions<DomolovOptions> options,
    IOptions<RetentionOptions> retention,
    IOptions<MatchOptions> match
)
{
    private static readonly string AppVersion =
        typeof(GetSettingsHandler)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0] ?? "dev";

    public SettingsResponse Handle()
    {
        var o = options.Value;
        var r = retention.Value;
        return new SettingsResponse(
            AppVersion,
            o.TimeZone,
            o.Role,
            o.MaxConcurrentScans,
            o.ScanCooldownMs,
            o.CloudflareChallengeWaitMs,
            o.BrowserHeadless,
            o.FakeProvider,
            !string.IsNullOrWhiteSpace(o.TelegramBotToken),
            !string.IsNullOrWhiteSpace(o.SmtpHost) && !string.IsNullOrWhiteSpace(o.SmtpFrom),
            !string.IsNullOrWhiteSpace(o.VapidPublicKey)
                && !string.IsNullOrWhiteSpace(o.VapidPrivateKey),
            o.VapidPublicKey,
            o.PublicUrl,
            new RetentionSettingsResponse(
                r.DelistedDays,
                r.OrphanListingDays,
                r.ScanRunDays,
                r.ScanRunKeepPerWatch,
                r.ArtifactDays,
                r.SessionDays,
                r.DailyAt
            ),
            new MatchSettingsResponse(match.Value.AutoLinkScore, match.Value.PossibleScore)
        );
    }
}
