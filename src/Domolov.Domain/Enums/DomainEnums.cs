namespace Domolov.Domain.Enums;

/// <summary>Lifecycle status of a ScanRun.</summary>
public enum ScanRunStatus
{
    Queued = 0,
    Running = 1,
    Baseline = 2,
    Succeeded = 3,
    Failed = 4,
}

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
}
