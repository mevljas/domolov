using System.ComponentModel.DataAnnotations;

namespace Domolov.Application.Options;

/// <summary>Which parts of Domolov this process runs.</summary>
public enum DomolovRole
{
    /// <summary>HTTP API and the scan worker in one process (local dev, small installs).</summary>
    All = 0,

    /// <summary>HTTP API only; no browser needed.</summary>
    Api = 1,

    /// <summary>Scheduler, scans and cleanup; serves only health endpoints.</summary>
    Worker = 2,
}

/// <summary>Core server options (bound from DOMOLOV_* environment variables).</summary>
public sealed class DomolovOptions
{
    public const string SectionName = "Domolov";

    public string AdminPassword { get; set; } = "";

    /// <summary>PBKDF2 hash from <c>hash-password</c>; takes precedence over AdminPassword.</summary>
    public string? AdminPasswordHash { get; set; }

    [Required]
    public string TimeZone { get; set; } = "Europe/Ljubljana";

    [Range(1, 8)]
    public int MaxConcurrentScans { get; set; } = 1;

    [Range(0, 3_600_000)]
    public int ScanCooldownMs { get; set; } = 20_000;

    [Range(0, 600_000)]
    public int CloudflareChallengeWaitMs { get; set; } = 30_000;

    public bool BrowserHeadless { get; set; } = true;

    [Required]
    public string BrowserUserDataDir { get; set; } = "browser-profile";

    public DomolovRole Role { get; set; } = DomolovRole.All;

    /// <summary>Registers the fixture provider (smoke tests and demo screenshots only).</summary>
    public bool FakeProvider { get; set; }

    /// <summary>Public base URL of the web app, used for links in notifications.</summary>
    public string? PublicUrl { get; set; }

    [Range(1, 365)]
    public int SessionLifetimeDays { get; set; } = 14;

    public string? TelegramBotToken { get; set; }
    public string? SmtpHost { get; set; }

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFrom { get; set; }
    public string? VapidPublicKey { get; set; }
    public string? VapidPrivateKey { get; set; }
    public string? VapidSubject { get; set; }

    public bool RunsApi => Role is DomolovRole.All or DomolovRole.Api;
    public bool RunsWorker => Role is DomolovRole.All or DomolovRole.Worker;

    public TimeZoneInfo ResolveTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
}

/// <summary>How long data is kept (DOMOLOV_RETENTION_*).</summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Domolov:Retention";

    /// <summary>Days after delisting before a non-Bookmarked Listing is purged.</summary>
    [Range(1, 3650)]
    public int DelistedDays { get; set; } = 30;

    /// <summary>Days a Listing no Watch sights any more is kept, unless Bookmarked.</summary>
    [Range(1, 3650)]
    public int OrphanListingDays { get; set; } = 30;

    [Range(1, 3650)]
    public int ScanRunDays { get; set; } = 90;

    [Range(0, 1000)]
    public int ScanRunKeepPerWatch { get; set; } = 20;

    [Range(1, 365)]
    public int ArtifactDays { get; set; } = 14;

    [Range(1, 365)]
    public int SessionDays { get; set; } = 30;

    /// <summary>Local time of the daily cleanup (HH:mm).</summary>
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$")]
    public string DailyAt { get; set; } = "04:00";
}

/// <summary>Home matching thresholds (DOMOLOV_MATCH_*).</summary>
public sealed class MatchOptions
{
    public const string SectionName = "Domolov:Match";

    [Range(0.0, 1.0)]
    public double AutoLinkScore { get; set; } = 0.85;

    [Range(0.0, 1.0)]
    public double PossibleScore { get; set; } = 0.6;

    [Range(0.0, 0.5)]
    public double SizeTolerance { get; set; } = 0.03;

    [Range(0.0, 0.5)]
    public double LandSizeTolerance { get; set; } = 0.05;
}
