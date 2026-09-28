using Domolov.Domain.Common;

namespace Domolov.Domain.Retention;

/// <summary>One execution of the retention cleanup, with how much it removed.</summary>
public sealed class CleanupRun
{
    private CleanupRun() { }

    public CleanupRun(bool isManual, DateTimeOffset now)
    {
        Id = Ids.New();
        IsManual = isManual;
        StartedAt = now;
    }

    public Guid Id { get; private set; }
    public bool IsManual { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public int DeletedListings { get; private set; }
    public int DeletedHomes { get; private set; }
    public int DeletedScanRuns { get; private set; }
    public int DeletedArtifacts { get; private set; }
    public int DeletedSessions { get; private set; }
    public long? BrowserProfileBytes { get; private set; }
    public string? Error { get; private set; }

    public void Finish(CleanupCounts counts, long? browserProfileBytes, DateTimeOffset now)
    {
        DeletedListings = counts.Listings;
        DeletedHomes = counts.Homes;
        DeletedScanRuns = counts.ScanRuns;
        DeletedArtifacts = counts.Artifacts;
        DeletedSessions = counts.Sessions;
        BrowserProfileBytes = browserProfileBytes;
        FinishedAt = now;
    }

    public void Fail(string error, DateTimeOffset now)
    {
        Error = error.Length <= 2000 ? error : error[..2000];
        FinishedAt = now;
    }
}

/// <summary>Rows removed by one cleanup.</summary>
public sealed record CleanupCounts(
    int Listings,
    int Homes,
    int ScanRuns,
    int Artifacts,
    int Sessions
);
