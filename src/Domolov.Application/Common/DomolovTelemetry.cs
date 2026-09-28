using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Domolov.Application.Common;

/// <summary>Activity source and metrics exported via OpenTelemetry.</summary>
public static class DomolovTelemetry
{
    public const string Name = "Domolov";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);

    public static readonly Histogram<double> ScanDuration = Meter.CreateHistogram<double>(
        "domolov.scan.duration",
        "s",
        "Duration of ScanRuns"
    );

    public static readonly Counter<long> ScanRuns = Meter.CreateCounter<long>(
        "domolov.scan.runs",
        description: "Finished ScanRuns by status"
    );

    public static readonly Counter<long> ListingChanges = Meter.CreateCounter<long>(
        "domolov.listings.changes",
        description: "New, repriced and reposted Listings"
    );

    public static readonly Counter<long> CloudflareChallenges = Meter.CreateCounter<long>(
        "domolov.cloudflare.challenges",
        description: "CloudflareChallenges encountered (outcome=cleared|blocked)"
    );

    public static readonly Histogram<double> CloudflareClearTime = Meter.CreateHistogram<double>(
        "domolov.cloudflare.clear_time",
        "s",
        "Time for a CloudflareChallenge to clear"
    );

    public static readonly Counter<long> NotificationFailures = Meter.CreateCounter<long>(
        "domolov.notifications.failures",
        description: "Notification sends that failed"
    );

    public static readonly Counter<long> CleanupDeleted = Meter.CreateCounter<long>(
        "domolov.cleanup.deleted",
        description: "Rows removed by retention (kind=listings|homes|scan_runs|artifacts|sessions)"
    );
}
