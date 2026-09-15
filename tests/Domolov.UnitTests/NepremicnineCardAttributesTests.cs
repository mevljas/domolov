using Domolov.Domain.Services;
using FluentAssertions;

namespace Domolov.UnitTests;

public sealed class NepremicnineCardAttributesTests
{
    [Fact]
    public void ParseCardAttributes_from_user_example()
    {
        var fixture = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "nepremicnine-card-kratek.txt")
        );
        var lines = fixture.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        var category = lines[0];
        var description = lines[2];

        var attrs = NepremicnineParsing.ParseCardAttributes(category, description);

        attrs.PropertyType.Should().Be("Stanovanje");
        attrs.Rooms.Should().Be("2-sobno");
        attrs.SizeText.Should().Be("73 m2");
        attrs.YearText.Should().Be("1925");
        attrs.FloorText.Should().Be("PK/1");
        attrs.LandSizeText.Should().Be("16 m2");
    }

    [Fact]
    public void ParseCardAttributes_house_subtitle()
    {
        var attrs = NepremicnineParsing.ParseCardAttributes(
            "Prodaja: Hiša, Samostojna",
            "120 m2, samostojna, grajena l. 2024, 450 m2 zemljišča, Hiša."
        );

        attrs.PropertyType.Should().Be("Hiša");
        attrs.Rooms.Should().Be("Samostojna");
        attrs.SizeText.Should().Be("120 m2");
        attrs.YearText.Should().Be("2024");
        attrs.LandSizeText.Should().Be("450 m2");
    }
}

public sealed class PriceFormattingTests
{
    [Theory]
    [InlineData(210_000, "EUR", "210.000 €")]
    [InlineData(210_000.5, "EUR", "210.000,50 €")]
    [InlineData(1_200, "EUR", "1.200 €")]
    public void Format_slovenian_compact(decimal amount, string currency, string expected)
    {
        PriceFormatting.Format(amount, currency).Should().Be(expected);
    }
}
