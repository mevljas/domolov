using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using Domolov.Infrastructure.Scanning;
using FluentAssertions;

namespace Domolov.UnitTests.Domain;

public sealed class HomeMatchScorerTests
{
    private static readonly MatchSettings Settings = new();

    private static MatchCandidate Candidate(
        string? description,
        long? photo = null,
        decimal? size = 72.4m,
        decimal? rooms = 3,
        string? floor = "3/5 nad.",
        string type = "Stanovanje",
        string title = "ljubljana bezigrad"
    ) =>
        new(
            type,
            rooms,
            size,
            null,
            "LJUBLJANA, BEŽIGRAD",
            2008,
            floor,
            photo,
            title,
            TextNormalizer.Normalize(description)
        );

    [Fact]
    public void Repost_with_reworded_text_but_same_photo_auto_links()
    {
        var a = Candidate("Prenovljeno stanovanje z novo kuhinjo, balkon, klet.", photo: 42);
        var b = Candidate(
            "Ponovno v prodaji: stanovanje po prenovi z novo kuhinjo, balkon in klet.",
            photo: 42 ^ 0b11
        );

        var result = HomeMatchScorer.Score(a, b, Settings)!;

        result.Score.Should().BeGreaterThanOrEqualTo(Settings.AutoLinkScore);
        result.Signals.PhotoDistance.Should().Be(2);
        result.Signals.MatchedAttributes.Should().Contain(["size", "rooms", "location", "floor"]);
    }

    [Fact]
    public void Different_size_is_a_hard_no_even_with_identical_text()
    {
        var a = Candidate("Enako besedilo.", size: 72.4m);
        var b = Candidate("Enako besedilo.", size: 80m);
        HomeMatchScorer.Score(a, b, Settings).Should().BeNull();
    }

    [Fact]
    public void Different_room_count_or_type_rules_the_pair_out()
    {
        HomeMatchScorer
            .Score(Candidate("x", rooms: 3), Candidate("x", rooms: 2), Settings)
            .Should()
            .BeNull();
        HomeMatchScorer
            .Score(Candidate("x"), Candidate("x", type: "Hiša"), Settings)
            .Should()
            .BeNull();
    }

    [Fact]
    public void Two_flats_in_one_building_with_shared_boilerplate_are_not_suggested()
    {
        const string boilerplate =
            "Agencija Dom ponuja stanovanje v novogradnji. Energijski razred B. Pokličite za ogled.";
        var a = Candidate(boilerplate + " Pogled na park.", photo: 1, floor: "2/5 nad.");
        var b = Candidate(
            boilerplate + " Pogled na dvorišče.",
            photo: long.MaxValue,
            floor: "4/5 nad."
        );

        HomeMatchScorer.Score(a, b, Settings)!.Score.Should().BeLessThan(Settings.PossibleScore);
    }

    [Fact]
    public void Price_is_not_part_of_the_score()
    {
        var a = Candidate("Isto stanovanje.", photo: 7);
        var b = Candidate("Isto stanovanje.", photo: 7);
        HomeMatchScorer.Score(a, b, Settings)!.Score.Should().Be(1);
    }

    [Fact]
    public void Missing_photos_renormalise_the_weights()
    {
        var a = Candidate("Družinsko stanovanje z vrtom in dvema parkirnima mestoma.");
        var b = Candidate("Družinsko stanovanje z vrtom in dvema parkirnima mestoma.");
        var result = HomeMatchScorer.Score(a, b, Settings)!;
        result.Signals.PhotoScore.Should().BeNull();
        result.Score.Should().Be(1);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(6, 1.0)]
    [InlineData(13, 0.5)]
    [InlineData(20, 0.0)]
    [InlineData(40, 0.0)]
    public void Photo_score_falls_off_with_distance(int distance, double expected) =>
        HomeMatchScorer.PhotoScoreFor(distance).Should().BeApproximately(expected, 0.001);

    /// <summary>The demo fixture must exercise every outcome: auto-link and review queue.</summary>
    [Theory]
    [InlineData("6411005", "6412105", "auto")]
    [InlineData("6411009", "6412109", "auto")]
    [InlineData("6411011", "6412111", "possible")]
    public void Fixture_pairs_score_as_designed(
        string originalId,
        string variantId,
        string expected
    )
    {
        var round1 = FixtureListingProvider.CardsForRound(1);
        var round4 = FixtureListingProvider.CardsForRound(4);
        var original = Listing.Create(
            Guid.NewGuid(),
            "fixture",
            round1.Single(c => c.ExternalId == originalId),
            TestData.Now
        );
        var variant = Listing.Create(
            Guid.NewGuid(),
            "fixture",
            round4.Single(c => c.ExternalId == variantId),
            TestData.Now
        );

        var score = HomeMatchScorer
            .Score(variant.ToMatchCandidate(), original.ToMatchCandidate(), Settings)!
            .Score;

        if (expected == "auto")
        {
            score.Should().BeGreaterThanOrEqualTo(Settings.AutoLinkScore);
        }
        else
        {
            score.Should().BeInRange(Settings.PossibleScore, Settings.AutoLinkScore - 0.0001);
        }
    }

    [Fact]
    public void Unrelated_fixture_listings_never_reach_the_review_threshold()
    {
        var cards = FixtureListingProvider.CardsForRound(1);
        var listings = cards
            .Select(c => Listing.Create(Guid.NewGuid(), "fixture", c, TestData.Now))
            .ToList();
        foreach (var a in listings)
        {
            foreach (var b in listings.Where(l => l != a))
            {
                var result = HomeMatchScorer.Score(
                    a.ToMatchCandidate(),
                    b.ToMatchCandidate(),
                    Settings
                );
                (result?.Score ?? 0)
                    .Should()
                    .BeLessThan(Settings.PossibleScore, $"{a.ExternalId} vs {b.ExternalId}");
            }
        }
    }
}

public sealed class TextAndHashTests
{
    [Theory]
    [InlineData("LJUBLJANA, BEŽIGRAD – Žale!", "ljubljana bezigrad zale")]
    [InlineData("Đurđa  čćšž", "durda ccsz")]
    [InlineData("  ,.  ", null)]
    public void Normalizer_folds_case_diacritics_and_punctuation(string input, string? expected) =>
        TextNormalizer.Normalize(input).Should().Be(expected);

    [Fact]
    public void Trigram_similarity_is_one_for_identical_and_zero_for_disjoint_text()
    {
        TrigramSimilarity.Compute("stanovanje z balkonom", "stanovanje z balkonom").Should().Be(1);
        TrigramSimilarity.Compute("abc", "xyz").Should().Be(0);
        TrigramSimilarity
            .Compute("stanovanje z balkonom", "stanovanje brez balkona")
            .Should()
            .BeInRange(0.3, 0.8);
    }

    [Fact]
    public void Dhash_distance_counts_differing_bits()
    {
        var gray = Enumerable.Range(0, 72).Select(i => (byte)(i * 3)).ToArray();
        var hash = PerceptualHash.DHash(gray);
        PerceptualHash.Distance(hash, hash).Should().Be(0);
        PerceptualHash.Distance(0, 0b1011).Should().Be(3);
        PerceptualHash.Distance(0, -1).Should().Be(64);
    }

    [Fact]
    public void Dhash_requires_a_nine_by_eight_thumbnail()
    {
        var act = () => PerceptualHash.DHash(new byte[10]);
        act.Should().Throw<ArgumentException>();
    }
}
