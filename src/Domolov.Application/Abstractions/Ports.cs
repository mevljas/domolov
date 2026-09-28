using Domolov.Application.Scans;
using Domolov.Domain.Notifications;
using Domolov.Domain.Providers;

namespace Domolov.Application.Abstractions;

/// <summary>Sends a notification payload on a specific channel.</summary>
public interface INotifier
{
    NotificationChannel Channel { get; }
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Resolves an IListingProvider by URL or id.</summary>
public interface IListingProviderResolver
{
    IListingProvider Resolve(Uri searchUrl);
    IListingProvider? TryGetById(string providerId);
    bool CanHandle(Uri searchUrl);
}

/// <summary>Cross-process signals (PostgreSQL LISTEN/NOTIFY in production).</summary>
public interface ISignalPublisher
{
    Task PublishAsync(
        string channel,
        string payload,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Signal channel names shared by the api and worker roles.</summary>
public static class SignalChannels
{
    /// <summary>A ScanRun was queued; wakes the worker's queue processor.</summary>
    public const string ScanRequested = "domolov_scan_requested";

    /// <summary>A ScanRun changed state; payload is the ScanRun id. Fans out to SSE clients.</summary>
    public const string ScanRunChanged = "domolov_scan_run_changed";

    /// <summary>The operator asked for a retention cleanup now.</summary>
    public const string CleanupRequested = "domolov_cleanup_requested";
}

/// <summary>Live ScanRun updates for Server-Sent Events subscribers.</summary>
public interface IScanRunEventHub
{
    IAsyncEnumerable<ScanRunResponse> SubscribeAsync(CancellationToken cancellationToken);
}

/// <summary>Database size statistics.</summary>
public interface IStorageStats
{
    Task<DatabaseStorage> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>Size of the database and its largest tables.</summary>
public sealed record DatabaseStorage(long DatabaseBytes, IReadOnlyList<TableStorage> Tables);

/// <summary>Size and approximate row count of one table.</summary>
public sealed record TableStorage(string Name, long Bytes, long ApproximateRows);
