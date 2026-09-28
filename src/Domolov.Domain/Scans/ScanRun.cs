using Domolov.Domain.Common;
using Domolov.Domain.Watches;

namespace Domolov.Domain.Scans;

/// <summary>Lifecycle status of a ScanRun.</summary>
public enum ScanRunStatus
{
    Queued = 0,
    Running = 1,
    Baseline = 2,
    Succeeded = 3,
    Failed = 4,

    /// <summary>The process stopped mid-scan; recovered at startup or shutdown.</summary>
    Interrupted = 5,
}

/// <summary>Counts produced by a ScanRun.</summary>
public sealed record ScanStats(
    int PagesScanned,
    int NewCount,
    int PriceChangeCount,
    int RepostCount
);

/// <summary>One execution of a Watch.</summary>
public sealed class ScanRun
{
    public const int ErrorMaxLength = 2000;

    private ScanRun() { }

    public ScanRun(Guid watchId, bool isManual, DateTimeOffset now)
    {
        Id = Ids.New();
        WatchId = watchId;
        IsManual = isManual;
        Status = ScanRunStatus.Queued;
        QueuedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid WatchId { get; private set; }
    public Watch? Watch { get; private set; }
    public ScanRunStatus Status { get; private set; }
    public bool IsManual { get; private set; }
    public DateTimeOffset QueuedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public int PagesScanned { get; private set; }
    public int NewCount { get; private set; }
    public int PriceChangeCount { get; private set; }
    public int RepostCount { get; private set; }
    public string? ErrorSummary { get; private set; }
    public string? NotifyErrorSummary { get; private set; }
    public bool CloudflareBlocked { get; private set; }

    /// <summary>True when the crawl reached the last results page.</summary>
    public bool CrawlReachedEnd { get; private set; }

    public bool IsActive => Status is ScanRunStatus.Queued or ScanRunStatus.Running;

    /// <summary>A successful non-baseline run that reached the last page; only these delist.</summary>
    public bool IsComplete => Status == ScanRunStatus.Succeeded && CrawlReachedEnd;

    public void Start(DateTimeOffset now)
    {
        if (Status != ScanRunStatus.Queued)
        {
            throw new InvalidOperationException($"ScanRun {Id} cannot start from {Status}.");
        }

        Status = ScanRunStatus.Running;
        StartedAt = now;
    }

    public void Succeed(bool baseline, ScanStats stats, bool crawlReachedEnd, DateTimeOffset now)
    {
        EnsureRunning();
        Status = baseline ? ScanRunStatus.Baseline : ScanRunStatus.Succeeded;
        PagesScanned = stats.PagesScanned;
        NewCount = stats.NewCount;
        PriceChangeCount = stats.PriceChangeCount;
        RepostCount = stats.RepostCount;
        CrawlReachedEnd = crawlReachedEnd;
        FinishedAt = now;
    }

    public void Fail(string error, bool cloudflareBlocked, int pagesScanned, DateTimeOffset now)
    {
        Status = ScanRunStatus.Failed;
        ErrorSummary = Truncate(error);
        CloudflareBlocked = cloudflareBlocked;
        PagesScanned = pagesScanned;
        FinishedAt = now;
    }

    public void Interrupt(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        Status = ScanRunStatus.Interrupted;
        ErrorSummary ??= "Interrupted: the worker stopped before the scan finished.";
        FinishedAt = now;
    }

    public void RecordNotificationErrors(IEnumerable<string> errors)
    {
        var list = errors.Distinct().Take(5).ToList();
        NotifyErrorSummary = list.Count == 0 ? null : Truncate(string.Join("; ", list));
    }

    private void EnsureRunning()
    {
        if (Status != ScanRunStatus.Running)
        {
            throw new InvalidOperationException($"ScanRun {Id} is not running ({Status}).");
        }
    }

    private static string Truncate(string value) =>
        value.Length <= ErrorMaxLength ? value : value[..ErrorMaxLength];
}
