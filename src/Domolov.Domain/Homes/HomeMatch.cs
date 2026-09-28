using Domolov.Domain.Common;

namespace Domolov.Domain.Homes;

/// <summary>Lifecycle of a HomeMatch.</summary>
public enum HomeMatchState
{
    /// <summary>Linked automatically because the score reached the auto-link threshold.</summary>
    AutoLinked = 0,

    /// <summary>Awaiting operator review; the Listing still has its own Home.</summary>
    Possible = 1,

    /// <summary>The operator confirmed the Listing belongs to the Home.</summary>
    Confirmed = 2,

    /// <summary>The operator said these are different homes; never suggested again.</summary>
    Rejected = 3,
}

/// <summary>Which signals contributed to a HomeMatch score.</summary>
public sealed class MatchSignals
{
    public int? PhotoDistance { get; init; }
    public double? PhotoScore { get; init; }
    public double? TitleSimilarity { get; init; }
    public double? DescriptionSimilarity { get; init; }
    public double? TextScore { get; init; }
    public double? AttributeScore { get; init; }
    public List<string> MatchedAttributes { get; init; } = [];
    public List<string> MismatchedAttributes { get; init; } = [];
}

/// <summary>The scored link between a Listing and a Home.</summary>
public sealed class HomeMatch
{
    private HomeMatch()
    {
        Signals = new MatchSignals();
    }

    private HomeMatch(
        Guid listingId,
        Guid homeId,
        double score,
        MatchSignals signals,
        HomeMatchState state,
        DateTimeOffset now
    )
    {
        Id = Ids.New();
        ListingId = listingId;
        HomeId = homeId;
        Score = Math.Round(score, 4);
        Signals = signals;
        State = state;
        CreatedAt = now;
        if (state is HomeMatchState.Confirmed or HomeMatchState.Rejected)
        {
            ReviewedAt = now;
        }
    }

    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid HomeId { get; private set; }
    public double Score { get; private set; }
    public MatchSignals Signals { get; private set; }
    public HomeMatchState State { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public static HomeMatch AutoLinked(
        Guid listingId,
        Guid homeId,
        MatchResult result,
        DateTimeOffset now
    ) => new(listingId, homeId, result.Score, result.Signals, HomeMatchState.AutoLinked, now);

    public static HomeMatch Possible(
        Guid listingId,
        Guid homeId,
        MatchResult result,
        DateTimeOffset now
    ) => new(listingId, homeId, result.Score, result.Signals, HomeMatchState.Possible, now);

    /// <summary>A manual link made by the operator.</summary>
    public static HomeMatch ManualLink(Guid listingId, Guid homeId, DateTimeOffset now) =>
        new(listingId, homeId, 1, new MatchSignals(), HomeMatchState.Confirmed, now);

    /// <summary>A pair the operator split apart; remembered so it is never suggested again.</summary>
    public static HomeMatch RejectedPair(Guid listingId, Guid homeId, DateTimeOffset now) =>
        new(listingId, homeId, 0, new MatchSignals(), HomeMatchState.Rejected, now);

    public void Confirm(DateTimeOffset now)
    {
        EnsurePending();
        State = HomeMatchState.Confirmed;
        ReviewedAt = now;
    }

    public void Reject(DateTimeOffset now)
    {
        if (State == HomeMatchState.Rejected)
        {
            return;
        }

        State = HomeMatchState.Rejected;
        ReviewedAt = now;
    }

    /// <summary>Points the match at the Home that absorbed its previous target.</summary>
    public void RetargetHome(Guid homeId) => HomeId = homeId;

    private void EnsurePending()
    {
        if (State != HomeMatchState.Possible)
        {
            throw new DomainRuleException("state", "Only possible matches can be confirmed.");
        }
    }
}
