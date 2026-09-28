using Domolov.Domain.Common;
using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using FluentAssertions;

namespace Domolov.UnitTests.Domain;

public sealed class HomeTests
{
    [Fact]
    public void Refresh_copies_the_primary_listing_and_counts()
    {
        var (home, listing) = TestData.HomeWith(TestData.Card(price: 300_000m));

        home.PrimaryListingId.Should().Be(listing.Id);
        home.Title.Should().Be(listing.Title);
        home.CurrentPrice.Should().Be(300_000m);
        home.SizeM2.Should().Be(72.4m);
        home.ListingCount.Should().Be(1);
        home.IsUnseen.Should().BeTrue();
        home.IsOffMarket.Should().BeFalse();
    }

    [Fact]
    public void Newest_active_listing_becomes_primary()
    {
        var (home, first) = TestData.HomeWith(TestData.Card("1", price: 300_000m));
        var second = Listing.Create(
            home.Id,
            "nepremicnine",
            TestData.Card("2", price: 295_000m),
            TestData.Now.AddDays(2)
        );

        home.Refresh([first, second], TestData.Now.AddDays(2));

        home.PrimaryListingId.Should().Be(second.Id);
        home.ActiveListingCount.Should().Be(2);
    }

    [Fact]
    public void Lower_price_makes_a_seen_home_unseen_again()
    {
        var (home, listing) = TestData.HomeWith(TestData.Card(price: 300_000m));
        home.MarkSeen(TestData.Now);

        listing.RecordPrice(280_000m, "EUR", TestData.Now.AddDays(1));
        var change = home.Refresh([listing], TestData.Now.AddDays(1));

        change.Should().Be(HomePriceChange.Decreased);
        home.IsUnseen.Should().BeTrue();
        home.PreviousPrice.Should().Be(300_000m);
    }

    [Fact]
    public void Higher_price_keeps_the_home_seen()
    {
        var (home, listing) = TestData.HomeWith(TestData.Card(price: 300_000m));
        home.MarkSeen(TestData.Now);

        listing.RecordPrice(310_000m, "EUR", TestData.Now.AddDays(1));
        home.Refresh([listing], TestData.Now.AddDays(1)).Should().Be(HomePriceChange.Increased);
        home.IsUnseen.Should().BeFalse();
    }

    [Fact]
    public void Home_goes_off_market_when_every_listing_is_delisted()
    {
        var (home, listing) = TestData.HomeWith(TestData.Card());
        listing.Delist(TestData.Now.AddDays(10));

        home.Refresh([listing], TestData.Now.AddDays(10));

        home.OffMarketAt.Should().Be(TestData.Now.AddDays(10));
    }

    [Fact]
    public void Dismissing_removes_the_bookmark_and_blocks_new_ones()
    {
        var (home, _) = TestData.HomeWith(TestData.Card());
        home.SetBookmark(BookmarkStage.Viewed, "nice", TestData.Now);

        home.Dismiss(TestData.Now);

        home.Bookmark.Should().BeNull();
        var act = () => home.SetBookmark(BookmarkStage.Interested, null, TestData.Now);
        act.Should().Throw<DomainRuleException>();
        home.Restore();
        home.SetBookmark(BookmarkStage.Interested, null, TestData.Now)
            .Stage.Should()
            .Be(BookmarkStage.Interested);
    }

    [Fact]
    public void Absorb_keeps_the_more_advanced_stage_and_joins_notes()
    {
        var (target, _) = TestData.HomeWith(TestData.Card("1"));
        var (other, _) = TestData.HomeWith(TestData.Card("2"));
        target.SetBookmark(BookmarkStage.Contacted, "Called the agent.", TestData.Now);
        other.SetBookmark(BookmarkStage.Viewed, "Great light.", TestData.Now);

        target.Absorb(other, TestData.Now.AddDays(1));

        target.Bookmark!.Stage.Should().Be(BookmarkStage.Viewed);
        target
            .Bookmark.Note.Should()
            .Contain("Called the agent.")
            .And.Contain("Great light.")
            .And.Contain("merged 2026-09-02");
    }

    [Fact]
    public void Absorb_moves_a_bookmark_onto_an_unbookmarked_home()
    {
        var (target, _) = TestData.HomeWith(TestData.Card("1"));
        var (other, _) = TestData.HomeWith(TestData.Card("2"));
        other.SetBookmark(BookmarkStage.OfferMade, null, TestData.Now);

        target.Absorb(other, TestData.Now);

        target.Bookmark!.Stage.Should().Be(BookmarkStage.OfferMade);
        target.Bookmark.HomeId.Should().Be(target.Id);
    }

    [Fact]
    public void Absorb_stays_dismissed_if_either_home_was()
    {
        var (target, _) = TestData.HomeWith(TestData.Card("1"));
        var (other, _) = TestData.HomeWith(TestData.Card("2"));
        target.SetBookmark(BookmarkStage.Interested, null, TestData.Now);
        other.Dismiss(TestData.Now);

        target.Absorb(other, TestData.Now);

        target.IsDismissed.Should().BeTrue();
        target.Bookmark.Should().BeNull();
    }

    [Fact]
    public void Bookmark_note_over_the_limit_is_rejected()
    {
        var (home, _) = TestData.HomeWith(TestData.Card());
        var act = () =>
            home.SetBookmark(
                BookmarkStage.Interested,
                new string('x', Bookmark.NoteMaxLength + 1),
                TestData.Now
            );
        act.Should().Throw<DomainRuleException>().Which.Field.Should().Be("note");
    }
}

public sealed class HomeListingClassifierTests
{
    [Fact]
    public void Classifies_original_repost_and_duplicate()
    {
        var homeId = Guid.NewGuid();
        var original = Listing.Create(homeId, "p", TestData.Card("1"), TestData.Now);
        var duplicate = Listing.Create(homeId, "p", TestData.Card("2"), TestData.Now.AddDays(1));
        original.Delist(TestData.Now.AddDays(5));
        duplicate.Delist(TestData.Now.AddDays(6));
        var repost = Listing.Create(homeId, "p", TestData.Card("3"), TestData.Now.AddDays(20));
        var all = new[] { original, duplicate, repost };

        HomeListingClassifier.RoleOf(original, all).Should().Be(HomeListingRole.Original);
        HomeListingClassifier.RoleOf(duplicate, all).Should().Be(HomeListingRole.Duplicate);
        HomeListingClassifier.RoleOf(repost, all).Should().Be(HomeListingRole.Repost);
        HomeListingClassifier.CountReposts(all).Should().Be(1);
        HomeListingClassifier.SelectPrimary(all).Should().Be(repost);
    }

    [Fact]
    public void Primary_falls_back_to_the_last_seen_listing_when_all_are_delisted()
    {
        var homeId = Guid.NewGuid();
        var a = Listing.Create(homeId, "p", TestData.Card("1"), TestData.Now);
        var b = Listing.Create(homeId, "p", TestData.Card("2"), TestData.Now);
        b.Refresh(TestData.Card("2"), TestData.Now.AddDays(3));
        a.Delist(TestData.Now.AddDays(4));
        b.Delist(TestData.Now.AddDays(4));

        HomeListingClassifier.SelectPrimary([a, b]).Should().Be(b);
    }
}
