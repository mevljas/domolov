using Domolov.Application.Abstractions;
using Domolov.Domain.Enums;
using Domolov.Infrastructure.Notifications;
using FluentAssertions;

namespace Domolov.UnitTests;

public sealed class NotificationContentTests
{
    [Fact]
    public void Discord_fields_include_metadata_and_formatted_price()
    {
        var message = new NotificationMessage(
            NotificationChannel.Discord,
            "https://discord.test/hook",
            "KODELJEVO",
            "New listing",
            "https://example.com/1",
            null,
            210_000m,
            null,
            "EUR",
            "KODELJEVO, BLIŽINA UKC",
            "Stanovanje",
            "2-sobno",
            "73 m2",
            "1925",
            "PK/1",
            "16 m2"
        );

        var fields = DiscordNotifier.BuildFields(message);
        fields.Should().HaveCount(8);

        var push = NotificationContent.BuildPushBody(message);
        push.Should().Contain("New listing");
        push.Should().Contain("KODELJEVO, BLIŽINA UKC");
        push.Should().Contain("210.000 €");
    }
}
