using Domolov.Domain.Scans;

namespace Domolov.Application.Scans;

/// <summary>A ScanRun as returned by the API and pushed over SSE.</summary>
public sealed record ScanRunResponse(
    Guid Id,
    Guid WatchId,
    string WatchName,
    ScanRunStatus Status,
    bool IsManual,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int PagesScanned,
    int NewCount,
    int PriceChangeCount,
    int RepostCount,
    string? ErrorSummary,
    string? NotifyErrorSummary,
    bool CloudflareBlocked,
    bool CrawlReachedEnd,
    int ArtifactCount
);

/// <summary>Metadata of a diagnostic artifact.</summary>
public sealed record ScanArtifactResponse(
    Guid Id,
    Guid ScanRunId,
    ScanArtifactKind Kind,
    string Label,
    string ContentType,
    int SizeBytes,
    DateTimeOffset CreatedAt
);

/// <summary>Artifact bytes for download.</summary>
public sealed record ScanArtifactContent(string ContentType, byte[] Content, string FileName);

/// <summary>Maps ScanRuns.</summary>
public static class ScanRunMapping
{
    public static ScanRunResponse ToResponse(ScanRun run, string watchName, int artifactCount) =>
        new(
            run.Id,
            run.WatchId,
            watchName,
            run.Status,
            run.IsManual,
            run.QueuedAt,
            run.StartedAt,
            run.FinishedAt,
            run.PagesScanned,
            run.NewCount,
            run.PriceChangeCount,
            run.RepostCount,
            run.ErrorSummary,
            run.NotifyErrorSummary,
            run.CloudflareBlocked,
            run.CrawlReachedEnd,
            artifactCount
        );
}
