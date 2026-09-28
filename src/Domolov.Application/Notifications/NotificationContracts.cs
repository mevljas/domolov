using System.ComponentModel.DataAnnotations;
using Domolov.Domain.Notifications;

namespace Domolov.Application.Notifications;

/// <summary>A NotificationRoute as returned by the API.</summary>
public sealed record NotificationRouteResponse(
    Guid Id,
    Guid WatchId,
    NotificationChannel Channel,
    string Destination,
    IReadOnlyList<NotificationTrigger> Triggers,
    bool IsEnabled,
    uint Version
);

/// <summary>Payload for adding a NotificationRoute.</summary>
public sealed record CreateNotificationRouteRequest(
    NotificationChannel Channel,
    [property: Required, MaxLength(2000)] string Destination,
    IReadOnlyList<NotificationTrigger>? Triggers = null,
    bool IsEnabled = true
);

/// <summary>Partial update of a NotificationRoute.</summary>
public sealed record UpdateNotificationRouteRequest(
    [property: MaxLength(2000)] string? Destination = null,
    IReadOnlyList<NotificationTrigger>? Triggers = null,
    bool? IsEnabled = null
);

/// <summary>Outcome of sending a test notification.</summary>
public sealed record TestDeliveryResponse(bool Ok, string? Error);
