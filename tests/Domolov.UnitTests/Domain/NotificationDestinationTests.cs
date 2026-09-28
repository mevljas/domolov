using Domolov.Domain.Common;
using Domolov.Domain.Notifications;
using FluentAssertions;

namespace Domolov.UnitTests.Domain;

public sealed class NotificationDestinationTests
{
    [Theory]
    [InlineData(NotificationChannel.Discord, "https://discord.com/api/webhooks/123/abc")]
    [InlineData(NotificationChannel.Discord, "https://discordapp.com/api/webhooks/123/abc")]
    [InlineData(NotificationChannel.Telegram, "-1001234567890")]
    [InlineData(NotificationChannel.Telegram, "@domolov_alerts")]
    [InlineData(NotificationChannel.Email, "hunter@example.com")]
    [InlineData(NotificationChannel.WebPush, "all")]
    public void Accepts_valid_destinations(NotificationChannel channel, string destination) =>
        NotificationDestination.Normalize(channel, $"  {destination} ").Should().Be(destination);

    [Theory]
    [InlineData(NotificationChannel.Discord, "https://example.com/hook")]
    [InlineData(NotificationChannel.Discord, "http://discord.com/api/webhooks/1/a")]
    [InlineData(NotificationChannel.Telegram, "my chat")]
    [InlineData(NotificationChannel.Email, "not-an-email")]
    [InlineData(NotificationChannel.Email, "")]
    public void Rejects_invalid_destinations(NotificationChannel channel, string destination)
    {
        var act = () => NotificationDestination.Normalize(channel, destination);
        act.Should().Throw<DomainRuleException>().Which.Field.Should().Be("destination");
    }
}
