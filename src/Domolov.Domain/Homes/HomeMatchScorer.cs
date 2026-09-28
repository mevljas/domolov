namespace Domolov.Domain.Homes;

/// <summary>The comparable facts of a Listing used to decide whether two ads are the same Home.</summary>
public sealed record MatchCandidate(
    string? PropertyType,
    decimal? RoomCount,
    decimal? SizeM2,
    decimal? LandSizeM2,
    string? Location,
    int? YearBuilt,
    string? FloorText,
    long? ImageHash,
    string? NormalizedTitle,
    string? NormalizedDescription
);

/// <summary>A scored comparison.</summary>
public sealed record MatchResult(double Score, MatchSignals Signals);

/// <summary>Tolerances and thresholds for Home matching.</summary>
public sealed record MatchSettings(
    double AutoLinkScore = 0.85,
    double PossibleScore = 0.6,
    decimal SizeTolerance = 0.03m,
    decimal LandSizeTolerance = 0.05m
);

/// <summary>
/// Scores whether two Listings advertise the same Home from the main photo, text and attributes.
/// Price is deliberately ignored: reposts usually change it.
/// </summary>
public static class HomeMatchScorer
{
    private const double PhotoWeight = 0.5;
    private const double AttributeWeight = 0.25;
    private const double TextWeight = 0.25;

    /// <summary>Hamming distance at or below which photos count as identical.</summary>
    public const int PhotoIdenticalDistance = 6;

    /// <summary>Hamming distance at or above which photos count as unrelated.</summary>
    public const int PhotoUnrelatedDistance = 20;

    /// <summary>Returns null when a hard constraint (type, rooms, size) rules the pair out.</summary>
    public static MatchResult? Score(MatchCandidate a, MatchCandidate b, MatchSettings settings)
    {
        if (!PassesHardConstraints(a, b, settings))
        {
            return null;
        }

        var matched = new List<string>();
        var mismatched = new List<string>();
        var attributeScore = ScoreAttributes(a, b, settings, matched, mismatched);

        int? distance = null;
        double? photoScore = null;
        if (a.ImageHash is long ha && b.ImageHash is long hb)
        {
            distance = PerceptualHash.Distance(ha, hb);
            photoScore = PhotoScoreFor(distance.Value);
        }

        var title = Similarity(a.NormalizedTitle, b.NormalizedTitle);
        var description = Similarity(a.NormalizedDescription, b.NormalizedDescription);
        double? textScore = (title, description) switch
        {
            (null, null) => null,
            (double t, null) => t,
            (null, double d) => d,
            (double t, double d) => (0.4 * t) + (0.6 * d),
        };

        var weighted = 0d;
        var totalWeight = 0d;
        Add(photoScore, PhotoWeight);
        Add(attributeScore, AttributeWeight);
        Add(textScore, TextWeight);

        var score = totalWeight == 0 ? 0 : weighted / totalWeight;
        var signals = new MatchSignals
        {
            PhotoDistance = distance,
            PhotoScore = Round(photoScore),
            TitleSimilarity = Round(title),
            DescriptionSimilarity = Round(description),
            TextScore = Round(textScore),
            AttributeScore = Round(attributeScore),
            MatchedAttributes = matched,
            MismatchedAttributes = mismatched,
        };
        return new MatchResult(Math.Round(score, 4), signals);

        void Add(double? value, double weight)
        {
            if (value is double v)
            {
                weighted += v * weight;
                totalWeight += weight;
            }
        }
    }

    public static bool PassesHardConstraints(
        MatchCandidate a,
        MatchCandidate b,
        MatchSettings settings
    )
    {
        if (
            a.PropertyType is { } ta
            && b.PropertyType is { } tb
            && !string.Equals(Fold(ta), Fold(tb), StringComparison.Ordinal)
        )
        {
            return false;
        }

        if (a.RoomCount is decimal ra && b.RoomCount is decimal rb && ra != rb)
        {
            return false;
        }

        return a.SizeM2 is not decimal sa
            || b.SizeM2 is not decimal sb
            || WithinTolerance(sa, sb, settings.SizeTolerance);
    }

    public static double PhotoScoreFor(int distance)
    {
        if (distance <= PhotoIdenticalDistance)
        {
            return 1;
        }

        if (distance >= PhotoUnrelatedDistance)
        {
            return 0;
        }

        return 1
            - (
                (double)(distance - PhotoIdenticalDistance)
                / (PhotoUnrelatedDistance - PhotoIdenticalDistance)
            );
    }

    private static double? ScoreAttributes(
        MatchCandidate a,
        MatchCandidate b,
        MatchSettings settings,
        List<string> matched,
        List<string> mismatched
    )
    {
        Compare(
            "size",
            a.SizeM2,
            b.SizeM2,
            (x, y) => WithinTolerance(x, y, settings.SizeTolerance)
        );
        Compare(
            "landSize",
            a.LandSizeM2,
            b.LandSizeM2,
            (x, y) => WithinTolerance(x, y, settings.LandSizeTolerance)
        );
        Compare("rooms", a.RoomCount, b.RoomCount, (x, y) => x == y);
        Compare("yearBuilt", a.YearBuilt, b.YearBuilt, (x, y) => x == y);
        CompareText("location", a.Location, b.Location);
        CompareText("floor", a.FloorText, b.FloorText);
        CompareText("propertyType", a.PropertyType, b.PropertyType);

        var total = matched.Count + mismatched.Count;
        return total == 0 ? null : (double)matched.Count / total;

        void Compare<T>(string name, T? x, T? y, Func<T, T, bool> equal)
            where T : struct
        {
            if (x is T vx && y is T vy)
            {
                (equal(vx, vy) ? matched : mismatched).Add(name);
            }
        }

        void CompareText(string name, string? x, string? y)
        {
            if (!string.IsNullOrWhiteSpace(x) && !string.IsNullOrWhiteSpace(y))
            {
                (
                    string.Equals(Fold(x), Fold(y), StringComparison.Ordinal) ? matched : mismatched
                ).Add(name);
            }
        }
    }

    private static double? Similarity(string? a, string? b) =>
        string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)
            ? null
            : TrigramSimilarity.Compute(a, b);

    private static bool WithinTolerance(decimal a, decimal b, decimal tolerance)
    {
        var larger = Math.Max(Math.Abs(a), Math.Abs(b));
        return larger == 0 || Math.Abs(a - b) / larger <= tolerance;
    }

    private static string Fold(string value) => TextNormalizer.Normalize(value) ?? "";

    private static double? Round(double? value) => value is double v ? Math.Round(v, 4) : null;
}
