using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Domolov.Application.Abstractions;
using Domolov.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;

namespace Domolov.Infrastructure.Notifications;

/// <summary>Discord incoming webhook notifier.</summary>
public sealed class DiscordNotifier(HttpClient http, ILogger<DiscordNotifier> logger) : INotifier
{
    public static readonly ActivitySource ActivitySource = new("Domolov.Notifications");

    public NotificationChannel Channel => NotificationChannel.Discord;

    public async Task SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity("DiscordNotify");
        var embed = new
        {
            embeds = new[]
            {
                new
                {
                    title = message.Title,
                    url = message.Url,
                    description = message.Body,
                    image = string.IsNullOrWhiteSpace(message.ImageUrl)
                        ? null
                        : new { url = message.ImageUrl },
                    fields = BuildFields(message),
                },
            },
        };

        using var response = await http.PostAsJsonAsync(
            message.Destination,
            embed,
            cancellationToken
        );
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Discord webhook failed: {Status} {Body}",
                response.StatusCode,
                body
            );
            response.EnsureSuccessStatusCode();
        }
    }

    private static object[] BuildFields(NotificationMessage message)
    {
        var fields = new List<object>();
        if (message.Price is decimal price)
        {
            fields.Add(new { name = "Price", value = $"{price} EUR", inline = true });
        }

        if (message.PreviousPrices is { Count: > 0 })
        {
            fields.Add(
                new
                {
                    name = "Previous",
                    value = string.Join(", ", message.PreviousPrices),
                    inline = true,
                }
            );
        }

        return fields.ToArray();
    }
}

/// <summary>Telegram Bot API notifier.</summary>
public sealed class TelegramNotifier(
    HttpClient http,
    IOptions<DomolovOptions> options,
    ILogger<TelegramNotifier> logger
) : INotifier
{
    public NotificationChannel Channel => NotificationChannel.Telegram;

    public async Task SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var token = options.Value.TelegramBotToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Telegram bot token is not configured.");
        }

        var text = new StringBuilder()
            .Append('*')
            .Append(Escape(message.Title))
            .Append('*')
            .AppendLine()
            .AppendLine(Escape(message.Body));
        if (!string.IsNullOrWhiteSpace(message.Url))
        {
            text.AppendLine(Escape(message.Url));
        }

        if (message.Price is decimal price)
        {
            text.AppendLine($"Price: {price} EUR");
        }

        var payload = new
        {
            chat_id = message.Destination,
            text = text.ToString(),
            parse_mode = "Markdown",
            disable_web_page_preview = false,
        };

        using var response = await http.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            payload,
            cancellationToken
        );
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Telegram send failed: {Status} {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }
    }

    private static string Escape(string value) =>
        value.Replace("_", "\\_", StringComparison.Ordinal).Replace("*", "\\*", StringComparison.Ordinal);
}

/// <summary>SMTP email notifier.</summary>
public sealed class EmailNotifier(IOptions<DomolovOptions> options, ILogger<EmailNotifier> logger)
    : INotifier
{
    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.SmtpHost) || string.IsNullOrWhiteSpace(o.SmtpFrom))
        {
            throw new InvalidOperationException("SMTP is not configured.");
        }

        using var client = new SmtpClient(o.SmtpHost, o.SmtpPort)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };
        if (!string.IsNullOrWhiteSpace(o.SmtpUser))
        {
            client.Credentials = new System.Net.NetworkCredential(o.SmtpUser, o.SmtpPassword);
        }

        using var mail = new MailMessage(o.SmtpFrom, message.Destination)
        {
            Subject = $"[Domolov] {message.Title}",
            Body =
                $"{message.Body}\n\n{message.Url}\nPrice: {message.Price} {message.PreviousPrices}",
            IsBodyHtml = false,
        };

        await client.SendMailAsync(mail, cancellationToken);
        logger.LogInformation("Email sent to {Destination}", message.Destination);
    }
}

/// <summary>Browser Web Push notifier.</summary>
public sealed class WebPushNotifier(
    IAppDbContext db,
    IOptions<DomolovOptions> options,
    ILogger<WebPushNotifier> logger
) : INotifier
{
    public NotificationChannel Channel => NotificationChannel.WebPush;

    public async Task SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var o = options.Value;
        if (
            string.IsNullOrWhiteSpace(o.VapidPublicKey)
            || string.IsNullOrWhiteSpace(o.VapidPrivateKey)
        )
        {
            throw new InvalidOperationException("VAPID keys are not configured.");
        }

        var client = new WebPushClient();
        var vapid = new VapidDetails(
            o.VapidSubject ?? "mailto:admin@localhost",
            o.VapidPublicKey,
            o.VapidPrivateKey
        );

        var payload = JsonSerializer.Serialize(
            new
            {
                title = message.Title,
                body = message.Body,
                url = message.Url,
            }
        );

        // Destination "all" sends to every stored subscription; otherwise match endpoint fragment.
        var subscriptions = db.PushSubscriptions.AsEnumerable().ToList();
        if (!string.Equals(message.Destination, "all", StringComparison.OrdinalIgnoreCase))
        {
            subscriptions = subscriptions
                .Where(s =>
                    s.Endpoint.Contains(message.Destination, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
        }

        foreach (var sub in subscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var push = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(push, payload, vapid);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Web push failed for {Endpoint}", sub.Endpoint);
            }
        }
    }
}
