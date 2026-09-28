using Domolov.Domain.Listings;
using Domolov.Domain.Watches;
using FluentAssertions;

namespace Domolov.UnitTests.Domain;

public sealed class ListingTests
{
    [Fact]
    public void Create_parses_attributes_normalises_text_and_records_the_first_price()
    {
        var listing = Listing.Create(
            Guid.NewGuid(),
            "nepremicnine",
            TestData.Card(price: 289_600m),
            TestData.Now
        );

        listing.SizeM2.Should().Be(72.4m);
        listing.RoomCount.Should().Be(3m);
        listing.YearBuilt.Should().Be(2008);
        listing.CurrentPrice.Should().Be(289_600m);
        listing.PreviousPrice.Should().BeNull();
        listing.PricePerM2.Should().Be(4000m);
        listing.NormalizedTitle.Should().Be("ljubljana bezigrad");
        listing.Prices.Should().ContainSingle();
    }

    [Fact]
    public void Price_drop_is_recorded_with_the_previous_price()
    {
        var listing = Listing.Create(
            Guid.NewGuid(),
            "nepremicnine",
            TestData.Card(price: 300_000m),
            TestData.Now
        );

        var kind = listing.RecordPrice(280_000m, "EUR", TestData.Now.AddDays(1));

        kind.Should().Be(ListingChangeKind.PriceDecreased);
        listing.PreviousPrice.Should().Be(300_000m);
        listing.CurrentPrice.Should().Be(280_000m);
        listing.PriceChangedAt.Should().Be(TestData.Now.AddDays(1));
        listing.Prices.Should().HaveCount(2);
    }

    [Fact]
    public void Same_or_missing_price_is_unchanged()
    {
        var listing = Listing.Create(
            Guid.NewGuid(),
            "nepremicnine",
            TestData.Card(price: 300_000m),
            TestData.Now
        );

        listing.RecordPrice(300_000m, "EUR", TestData.Now).Should().Be(ListingChangeKind.Unchanged);
        listing.RecordPrice(null, "EUR", TestData.Now).Should().Be(ListingChangeKind.Unchanged);
        listing.Prices.Should().ContainSingle();
    }

    [Fact]
    public void Refresh_keeps_known_fields_when_the_card_omits_them_and_relists()
    {
        var listing = Listing.Create(Guid.NewGuid(), "nepremicnine", TestData.Card(), TestData.Now);
        listing.Delist(TestData.Now.AddDays(3));

        listing.Refresh(
            TestData.Card(description: null, size: null) with
            {
                ImageUrl = null,
            },
            TestData.Now.AddDays(5)
        );

        listing.IsDelisted.Should().BeFalse();
        listing.SizeText.Should().Be("72.4 m2");
        listing.ImageUrl.Should().NotBeNull();
        listing.LastSeenAt.Should().Be(TestData.Now.AddDays(5));
    }

    [Fact]
    public void Delisting_keeps_the_first_delisted_time()
    {
        var listing = Listing.Create(Guid.NewGuid(), "nepremicnine", TestData.Card(), TestData.Now);
        listing.Delist(TestData.Now.AddDays(1));
        listing.Delist(TestData.Now.AddDays(2));
        listing.DelistedAt.Should().Be(TestData.Now.AddDays(1));
    }
}

public sealed class ListingAttributeParserTests
{
    [Theory]
    [InlineData("72.4 m2", 72.4)]
    [InlineData("54,2 m²", 54.2)]
    [InlineData("1.250 m2", 1250)]
    [InlineData("186 m2", 186)]
    public void Parses_areas(string text, double expected) =>
        ListingAttributeParser.ParseArea(text).Should().Be((decimal)expected);

    [Theory]
    [InlineData("2,5-sobno", 2.5)]
    [InlineData("3-sobno", 3)]
    [InlineData("5-sobna", 5)]
    [InlineData("garsonjera", 1)]
    public void Parses_rooms(string text, double expected) =>
        ListingAttributeParser.ParseRooms(text).Should().Be((decimal)expected);

    [Theory]
    [InlineData("2008", 2008)]
    [InlineData("zgrajeno l. 1978, prenovljeno 2019", 1978)]
    public void Parses_year(string text, int expected) =>
        ListingAttributeParser.ParseYear(text).Should().Be(expected);

    [Fact]
    public void Blank_input_yields_nulls() =>
        ListingAttributeParser
            .Parse(null, " ", null, "")
            .Should()
            .Be(new ParsedAttributes(null, null, null, null));
}

public sealed class DelistingPolicyTests
{
    private static WatchSighting Sighting(int missed, bool stale = false)
    {
        var s = new WatchSighting(Guid.NewGuid(), Guid.NewGuid(), TestData.Now);
        if (stale)
        {
            s.MarkStale();
        }

        for (var i = 0; i < missed; i++)
        {
            s.Missed();
        }

        return s;
    }

    [Fact]
    public void Two_misses_by_every_watch_delist() =>
        DelistingPolicy.ShouldDelist([Sighting(2), Sighting(3)]).Should().BeTrue();

    [Fact]
    public void One_watch_still_seeing_it_keeps_it_listed() =>
        DelistingPolicy.ShouldDelist([Sighting(2), Sighting(1)]).Should().BeFalse();

    [Fact]
    public void A_single_miss_is_not_enough() =>
        DelistingPolicy.ShouldDelist([Sighting(1)]).Should().BeFalse();

    [Fact]
    public void Stale_sightings_do_not_count()
    {
        DelistingPolicy.ShouldDelist([Sighting(0, stale: true), Sighting(2)]).Should().BeTrue();
        DelistingPolicy.ShouldDelist([Sighting(5, stale: true)]).Should().BeFalse();
    }

    [Fact]
    public void Seeing_it_again_resets_misses_and_staleness()
    {
        var s = Sighting(3, stale: true);
        s.Seen(TestData.Now.AddDays(1));
        s.MissedRunCount.Should().Be(0);
        s.IsStale.Should().BeFalse();
    }
}
