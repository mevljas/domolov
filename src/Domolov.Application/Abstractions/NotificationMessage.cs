using Domolov.Domain.Listings;
using Domolov.Domain.Notifications;

namespace Domolov.Application.Abstractions;

/// <summary>Outbound notification content.</summary>
public sealed record NotificationMessage(
    NotificationChannel Channel,
    string Destination,
    string Title,
    string Body,
    string? Url,
    string? ImageUrl,
    decimal? Price,
    IReadOnlyList<decimal>? PreviousPrices,
    string? Currency = null,
    string? Location = null,
    string? PropertyType = null,
    string? Rooms = null,
    string? SizeText = null,
    string? YearText = null,
    string? FloorText = null,
    string? LandSizeText = null,
    string? Description = null,
    ListingChangeKind Kind = ListingChangeKind.New,
    Guid? HomeId = null,
    string? AppUrl = null
)
{
    /// <summary>App-relative path of the Home, used by Web Push click handling.</summary>
    public string? AppPath => HomeId is { } id ? $"/homes/{id}" : null;
}
