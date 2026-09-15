using System.Text;
using Domolov.Application.Abstractions;
using Domolov.Domain.Services;

namespace Domolov.Infrastructure.Notifications;

/// <summary>Shared formatting for outbound notification channels.</summary>
public static class NotificationContent
{
    public static string FormatPrice(decimal amount, string? currency) =>
        PriceFormatting.Format(amount, currency);

    public static void AppendMetadataLines(StringBuilder text, NotificationMessage message)
    {
        foreach (var (label, value) in MetadataPairs(message))
        {
            text.Append(label).Append(": ").Append(value).AppendLine();
        }

        if (message.Price is decimal price)
        {
            text.Append("Price: ").Append(FormatPrice(price, message.Currency)).AppendLine();
        }

        if (message.PreviousPrices is { Count: > 0 })
        {
            text.Append("Previous: ")
                .Append(
                    string.Join(
                        ", ",
                        message.PreviousPrices.Select(p => FormatPrice(p, message.Currency))
                    )
                )
                .AppendLine();
        }
    }

    public static string BuildPushBody(NotificationMessage message)
    {
        var sb = new StringBuilder();
        sb.Append(message.Body);
        if (!string.IsNullOrWhiteSpace(message.Location))
        {
            sb.Append(" · ").Append(message.Location);
        }

        if (message.Price is decimal price)
        {
            sb.Append(" · ").Append(FormatPrice(price, message.Currency));
        }

        return sb.ToString();
    }

    public static IEnumerable<(string Name, string Value)> MetadataPairs(
        NotificationMessage message
    )
    {
        if (!string.IsNullOrWhiteSpace(message.Location))
        {
            yield return ("Location", message.Location);
        }

        if (!string.IsNullOrWhiteSpace(message.PropertyType))
        {
            yield return ("Type", message.PropertyType);
        }

        if (!string.IsNullOrWhiteSpace(message.Rooms))
        {
            yield return ("Rooms", message.Rooms);
        }

        if (!string.IsNullOrWhiteSpace(message.SizeText))
        {
            yield return ("Size", message.SizeText);
        }

        if (!string.IsNullOrWhiteSpace(message.YearText))
        {
            yield return ("Year", message.YearText);
        }

        if (!string.IsNullOrWhiteSpace(message.FloorText))
        {
            yield return ("Floor", message.FloorText);
        }

        if (!string.IsNullOrWhiteSpace(message.LandSizeText))
        {
            yield return ("Land", message.LandSizeText);
        }
    }
}
