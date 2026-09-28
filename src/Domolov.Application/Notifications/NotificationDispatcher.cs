using Domolov.Application.Abstractions;
using Domolov.Application.Options;
using Domolov.Domain.Common;
using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using Domolov.Domain.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domolov.Application.Notifications;

/// <summary>Something that happened to a Home during a ScanRun and may be notified.</summary>
public sealed record HomeEvent(
    ListingChangeKind Kind,
    Home Home,
    Listing Listing,
    decimal? PreviousPrice,
    DateTimeOffset? PreviousAdEndedAt = null
);

/// <summary>Builds messages for Home events and sends them to matching routes.</summary>
public sealed class NotificationDispatcher(
    IEnumerable<INotifier> notifiers,
    IOptions<DomolovOptions> options,
    TimeProvider clock,
    ILogger<NotificationDispatcher> logger
)
{
    private readonly Dictionary<NotificationChannel, INotifier> _notifiers = notifiers
        .GroupBy(n => n.Channel)
        .ToDictionary(g => g.Key, g => g.First());

    /// <summary>Sends every event to every enabled route whose triggers match. Returns errors.</summary>
    public async Task<IReadOnlyList<string>> DispatchAsync(
        IReadOnlyCollection<NotificationRoute> routes,
        IReadOnlyCollection<HomeEvent> events,
        CancellationToken cancellationToken
    )
    {
        var errors = new List<string>();
        foreach (var evt in events.Where(e => !e.Home.IsDismissed))
        {
            foreach (var route in routes.Where(r => r.IsEnabled))
            {
                if (!ListingDiffService.MatchesTrigger(route.Triggers, evt.Kind))
                {
                    continue;
                }

                var error = await SendAsync(route, Compose(route, evt), cancellationToken);
                if (error is not null)
                {
                    errors.Add($"{route.Channel}: {error}");
                }
            }
        }

        return errors;
    }

    public Task<string?> SendTestAsync(NotificationRoute route, CancellationToken cancellationToken)
    {
        var message = new NotificationMessage(
            route.Channel,
            route.Destination,
            "Domolov test notification",
            "If you can read this, this route works. Happy hunting!",
            PublicUrl("/"),
            null,
            285_000m,
            [299_000m],
            "EUR",
            "Ljubljana, Bežigrad",
            "Stanovanje",
            "3-sobno",
            "72.4 m2",
            "2008",
            "3/5",
            Kind: ListingChangeKind.PriceDecreased
        );
        return SendAsync(route, message, cancellationToken);
    }

    public NotificationMessage Compose(NotificationRoute route, HomeEvent evt)
    {
        var listing = evt.Listing;
        var price = evt.Home.CurrentPrice ?? listing.CurrentPrice;
        var body = evt.Kind switch
        {
            ListingChangeKind.New => "New listing",
            ListingChangeKind.Reposted => RepostBody(evt, price),
            ListingChangeKind.PriceDecreased or ListingChangeKind.PriceIncreased => PriceBody(
                evt,
                price
            ),
            _ => "Listing update",
        };

        return new NotificationMessage(
            route.Channel,
            route.Destination,
            listing.Title,
            body,
            listing.Url,
            listing.ImageUrl,
            price,
            evt.PreviousPrice is decimal p ? [p] : null,
            listing.Currency,
            listing.Location,
            listing.PropertyType,
            listing.Rooms,
            listing.SizeText,
            listing.YearText,
            listing.FloorText,
            listing.LandSizeText,
            listing.Description,
            evt.Kind,
            evt.Home.Id,
            PublicUrl($"/homes/{evt.Home.Id}")
        );
    }

    private string RepostBody(HomeEvent evt, decimal? price)
    {
        var gap = evt.PreviousAdEndedAt is { } ended ? clock.GetUtcNow() - ended : (TimeSpan?)null;
        var when = gap is { } g ? $" after {Humanize(g)}" : "";
        if (evt.PreviousPrice is decimal before && price is decimal now && before != now)
        {
            var diff = PriceFormatting.Format(Math.Abs(before - now), evt.Listing.Currency);
            var direction = now < before ? "cheaper" : "more expensive";
            return $"Reposted{when}, {diff} {direction} (was {PriceFormatting.Format(before, evt.Listing.Currency)})";
        }

        return $"Reposted{when} at the same price";
    }

    private static string PriceBody(HomeEvent evt, decimal? price)
    {
        if (evt.PreviousPrice is not decimal before || price is not decimal now || before == 0)
        {
            return "Price changed";
        }

        var verb = now < before ? "dropped" : "rose";
        var pct = Math.Abs((now - before) / before * 100m);
        return $"Price {verb} from {PriceFormatting.Format(before, evt.Listing.Currency)} to {PriceFormatting.Format(now, evt.Listing.Currency)} ({pct:0.#} %)";
    }

    private static string Humanize(TimeSpan span) =>
        span.TotalDays >= 14 ? $"{(int)(span.TotalDays / 7)} weeks"
        : span.TotalDays >= 2 ? $"{(int)span.TotalDays} days"
        : span.TotalHours >= 2 ? $"{(int)span.TotalHours} hours"
        : "a moment";

    private string? PublicUrl(string path) =>
        string.IsNullOrWhiteSpace(options.Value.PublicUrl)
            ? null
            : options.Value.PublicUrl.TrimEnd('/') + path;

    private async Task<string?> SendAsync(
        NotificationRoute route,
        NotificationMessage message,
        CancellationToken cancellationToken
    )
    {
        if (!_notifiers.TryGetValue(route.Channel, out var notifier))
        {
            return $"No notifier registered for {route.Channel}.";
        }

        try
        {
            await notifier.SendAsync(message, cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Notification failed for route {RouteId} ({Channel})",
                route.Id,
                route.Channel
            );
            return ex.Message;
        }
    }
}
