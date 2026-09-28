using Domolov.Domain.Common;

namespace Domolov.Domain.Notifications;

/// <summary>Outbound notification channel type.</summary>
public enum NotificationChannel
{
    Discord = 0,
    Telegram = 1,
    Email = 2,
    WebPush = 3,
}

/// <summary>Flags describing when a NotificationRoute should fire.</summary>
[Flags]
public enum NotificationTrigger
{
    None = 0,
    NewListing = 1,
    PriceDecreased = 2,
    PriceIncreased = 4,
    AnyPriceChange = 8,
    Reposted = 16,
}

/// <summary>A per-Watch delivery rule selecting channel, destination and triggers.</summary>
public sealed class NotificationRoute
{
    public const int DestinationMaxLength = 2000;

    public const NotificationTrigger DefaultTriggers =
        NotificationTrigger.NewListing
        | NotificationTrigger.PriceDecreased
        | NotificationTrigger.Reposted;

    private NotificationRoute()
    {
        Destination = "";
    }

    internal NotificationRoute(
        Guid watchId,
        NotificationChannel channel,
        string destination,
        NotificationTrigger triggers,
        bool isEnabled
    )
    {
        Id = Ids.New();
        WatchId = watchId;
        Channel = channel;
        Destination = NotificationDestination.Normalize(channel, destination);
        Triggers = RequireTriggers(triggers);
        IsEnabled = isEnabled;
    }

    public Guid Id { get; private set; }
    public Guid WatchId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Destination { get; private set; }
    public NotificationTrigger Triggers { get; private set; }
    public bool IsEnabled { get; private set; }
    public uint Version { get; private set; }

    public void Update(string? destination, NotificationTrigger? triggers, bool? isEnabled)
    {
        if (destination is not null)
        {
            Destination = NotificationDestination.Normalize(Channel, destination);
        }

        if (triggers is { } t)
        {
            Triggers = RequireTriggers(t);
        }

        if (isEnabled is { } enabled)
        {
            IsEnabled = enabled;
        }
    }

    private static NotificationTrigger RequireTriggers(NotificationTrigger triggers) =>
        triggers == NotificationTrigger.None
            ? throw new DomainRuleException("triggers", "Pick at least one trigger.")
            : triggers;
}
