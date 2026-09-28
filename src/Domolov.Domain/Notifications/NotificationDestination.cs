using System.Net.Mail;
using System.Text.RegularExpressions;
using Domolov.Domain.Common;

namespace Domolov.Domain.Notifications;

/// <summary>Channel-specific validation for NotificationRoute destinations.</summary>
public static partial class NotificationDestination
{
    public const string AllPushSubscriptions = "all";

    public static string Normalize(NotificationChannel channel, string? destination)
    {
        var value = destination?.Trim() ?? "";
        if (value.Length == 0)
        {
            throw new DomainRuleException("destination", "Destination is required.");
        }

        if (value.Length > NotificationRoute.DestinationMaxLength)
        {
            throw new DomainRuleException("destination", "Destination is too long.");
        }

        var error = channel switch
        {
            NotificationChannel.Discord => IsDiscordWebhook(value)
                ? null
                : "Use a Discord incoming webhook URL (https://discord.com/api/webhooks/...).",
            NotificationChannel.Telegram => TelegramChatRegex().IsMatch(value)
                ? null
                : "Use a numeric Telegram chat id (e.g. -1001234567890) or @channelname.",
            NotificationChannel.Email => MailAddress.TryCreate(value, out _)
                ? null
                : "Use a valid email address.",
            NotificationChannel.WebPush => null,
            _ => "Unknown channel.",
        };

        return error is null ? value : throw new DomainRuleException("destination", error);
    }

    private static bool IsDiscordWebhook(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (
            uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".discord.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)
        )
        && uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^(-?\d{3,20}|@[A-Za-z][A-Za-z0-9_]{3,})$", RegexOptions.CultureInvariant)]
    private static partial Regex TelegramChatRegex();
}
