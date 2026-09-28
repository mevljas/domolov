using Domolov.Domain.Common;
using Domolov.Domain.Notifications;

namespace Domolov.Domain.Watches;

/// <summary>A saved crawl target: provider, search URL, schedule and notification routes.</summary>
public sealed class Watch
{
    public const int NameMaxLength = 200;
    public const int SearchUrlMaxLength = 2000;

    private readonly List<NotificationRoute> _notificationRoutes = [];

    private Watch()
    {
        Name = "";
        ProviderId = "";
        SearchUrl = "";
        Cron = WatchCronSchedule.Every6HoursCron;
    }

    public Watch(string name, string providerId, string searchUrl, string cron, DateTimeOffset now)
    {
        Id = Ids.New();
        Name = NormalizeName(name);
        ProviderId = providerId;
        SearchUrl = NormalizeUrl(searchUrl);
        Cron = WatchCronSchedule.Normalize(cron);
        CreatedAt = now;
        SearchUrlChangedAt = now;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string ProviderId { get; private set; }
    public string SearchUrl { get; private set; }
    public string Cron { get; private set; }

    /// <summary>A Paused Watch is skipped by the scheduler; Run now still works.</summary>
    public bool IsPaused { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastScannedAt { get; private set; }
    public DateTimeOffset SearchUrlChangedAt { get; private set; }
    public bool HasCompletedBaseline { get; private set; }
    public int CloudflareStrikeCount { get; private set; }
    public DateTimeOffset? CloudflareBlockedUntil { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL xmin).</summary>
    public uint Version { get; private set; }

    public IReadOnlyCollection<NotificationRoute> NotificationRoutes => _notificationRoutes;

    public void Rename(string name) => Name = NormalizeName(name);

    public void Reschedule(string cron) => Cron = WatchCronSchedule.Normalize(cron);

    /// <summary>
    /// Points the Watch at a new search. Returns true when the URL changed; earlier sightings
    /// then no longer count for delisting and the next successful scan is a fresh baseline.
    /// </summary>
    public bool ChangeSearchUrl(string searchUrl, string providerId, DateTimeOffset now)
    {
        var normalized = NormalizeUrl(searchUrl);
        if (string.Equals(normalized, SearchUrl, StringComparison.Ordinal))
        {
            return false;
        }

        SearchUrl = normalized;
        ProviderId = providerId;
        SearchUrlChangedAt = now;
        HasCompletedBaseline = false;
        return true;
    }

    public void Pause() => IsPaused = true;

    public void Resume() => IsPaused = false;

    public void RecordScanFinished(DateTimeOffset now) => LastScannedAt = now;

    public void CompleteBaseline() => HasCompletedBaseline = true;

    public bool IsCloudflareBlocked(DateTimeOffset now) =>
        CloudflareBlockedUntil is { } until && until > now;

    public void ApplyCloudflareStrike(DateTimeOffset now)
    {
        CloudflareStrikeCount = Math.Max(0, CloudflareStrikeCount) + 1;
        CloudflareBlockedUntil = now.Add(CloudflareBackoff.DelayForStrike(CloudflareStrikeCount));
    }

    public void ClearCloudflareBackoff()
    {
        CloudflareStrikeCount = 0;
        CloudflareBlockedUntil = null;
    }

    public NotificationRoute AddRoute(
        NotificationChannel channel,
        string destination,
        NotificationTrigger triggers,
        bool isEnabled
    )
    {
        var route = new NotificationRoute(Id, channel, destination, triggers, isEnabled);
        _notificationRoutes.Add(route);
        return route;
    }

    public NotificationRoute? FindRoute(Guid routeId) =>
        _notificationRoutes.FirstOrDefault(r => r.Id == routeId);

    public bool RemoveRoute(Guid routeId)
    {
        var route = FindRoute(routeId);
        return route is not null && _notificationRoutes.Remove(route);
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            throw new DomainRuleException("name", "Name is required.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainRuleException(
                "name",
                $"Name must be at most {NameMaxLength} characters."
            );
        }

        return trimmed;
    }

    private static string NormalizeUrl(string searchUrl)
    {
        var trimmed = searchUrl?.Trim() ?? "";
        if (
            !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        )
        {
            throw new DomainRuleException(
                "searchUrl",
                "Search URL must be an absolute http(s) URL."
            );
        }

        if (trimmed.Length > SearchUrlMaxLength)
        {
            throw new DomainRuleException(
                "searchUrl",
                $"Search URL must be at most {SearchUrlMaxLength} characters."
            );
        }

        return trimmed;
    }
}
