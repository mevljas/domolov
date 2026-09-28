using Domolov.Application.Abstractions;
using Domolov.Application.Options;
using Domolov.Application.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Domolov.Application.Retention;

/// <summary>A finished or running cleanup.</summary>
public sealed record CleanupRunResponse(
    Guid Id,
    bool IsManual,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int DeletedListings,
    int DeletedHomes,
    int DeletedScanRuns,
    int DeletedArtifacts,
    int DeletedSessions,
    long? BrowserProfileBytes,
    string? Error
);

/// <summary>Storage usage and cleanup status.</summary>
public sealed record StorageResponse(
    long DatabaseBytes,
    IReadOnlyList<TableStorage> Tables,
    CleanupRunResponse? LastCleanup,
    DateTimeOffset? NextCleanupAt,
    RetentionSettingsResponse Retention
);

/// <summary>Reports storage usage.</summary>
public sealed class GetStorageHandler(
    IAppDbContext db,
    IStorageStats stats,
    IOptions<DomolovOptions> options,
    IOptions<RetentionOptions> retention,
    TimeProvider clock
)
{
    public async Task<StorageResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var database = await stats.GetAsync(cancellationToken);
        var last = await db
            .CleanupRuns.AsNoTracking()
            .OrderByDescending(c => c.StartedAt)
            .Select(c => new CleanupRunResponse(
                c.Id,
                c.IsManual,
                c.StartedAt,
                c.FinishedAt,
                c.DeletedListings,
                c.DeletedHomes,
                c.DeletedScanRuns,
                c.DeletedArtifacts,
                c.DeletedSessions,
                c.BrowserProfileBytes,
                c.Error
            ))
            .FirstOrDefaultAsync(cancellationToken);
        var r = retention.Value;
        return new StorageResponse(
            database.DatabaseBytes,
            database.Tables,
            last,
            CleanupSchedule.Next(r.DailyAt, clock.GetUtcNow(), options.Value.ResolveTimeZone()),
            new RetentionSettingsResponse(
                r.DelistedDays,
                r.OrphanListingDays,
                r.ScanRunDays,
                r.ScanRunKeepPerWatch,
                r.ArtifactDays,
                r.SessionDays,
                r.DailyAt
            )
        );
    }
}

/// <summary>Asks the worker to run a cleanup now.</summary>
public sealed class RequestCleanupHandler(ISignalPublisher signals)
{
    public Task HandleAsync(CancellationToken cancellationToken) =>
        signals.PublishAsync(SignalChannels.CleanupRequested, "manual", cancellationToken);
}

/// <summary>When the next daily cleanup runs.</summary>
public static class CleanupSchedule
{
    public static DateTimeOffset Next(string dailyAt, DateTimeOffset nowUtc, TimeZoneInfo timeZone)
    {
        var time = TimeOnly.ParseExact(dailyAt, "HH:mm");
        var local = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var candidate = new DateTime(
            DateOnly.FromDateTime(local.DateTime),
            time,
            DateTimeKind.Unspecified
        );
        if (candidate <= local.DateTime)
        {
            candidate = candidate.AddDays(1);
        }

        return new DateTimeOffset(candidate, timeZone.GetUtcOffset(candidate)).ToUniversalTime();
    }
}
