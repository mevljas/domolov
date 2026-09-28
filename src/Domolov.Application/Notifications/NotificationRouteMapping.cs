using Domolov.Domain.Notifications;

namespace Domolov.Application.Notifications;

/// <summary>Maps routes and converts trigger lists to and from flags.</summary>
public static class NotificationRouteMapping
{
    private static readonly NotificationTrigger[] AllTriggers =
    [
        NotificationTrigger.NewListing,
        NotificationTrigger.PriceDecreased,
        NotificationTrigger.PriceIncreased,
        NotificationTrigger.AnyPriceChange,
        NotificationTrigger.Reposted,
    ];

    public static NotificationRouteResponse ToResponse(NotificationRoute route) =>
        new(
            route.Id,
            route.WatchId,
            route.Channel,
            route.Destination,
            ToList(route.Triggers),
            route.IsEnabled,
            route.Version
        );

    public static IReadOnlyList<NotificationTrigger> ToList(NotificationTrigger flags) =>
        AllTriggers.Where(t => flags.HasFlag(t)).ToList();

    public static NotificationTrigger ToFlags(IReadOnlyList<NotificationTrigger>? triggers) =>
        triggers is null
            ? NotificationRoute.DefaultTriggers
            : triggers.Aggregate(NotificationTrigger.None, (acc, t) => acc | t);
}
