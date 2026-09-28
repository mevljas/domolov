using Domolov.Application.Abstractions;
using Domolov.Application.Options;
using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domolov.Application.Homes;

/// <summary>What happened when a new Listing was matched against existing Homes.</summary>
public sealed record MatchOutcome(
    Home? LinkedHome,
    HomeListingRole Role,
    decimal? PreviousPrice,
    DateTimeOffset? PreviousAdEndedAt,
    int PossibleMatches
)
{
    public static readonly MatchOutcome None = new(null, HomeListingRole.Original, null, null, 0);

    public bool IsLinked => LinkedHome is not null;
}

/// <summary>
/// Detects Reposts and Duplicates: prefilters candidate Listings in SQL by type, rooms and size,
/// scores them with <see cref="HomeMatchScorer"/>, then auto-links or records Possible matches.
/// </summary>
public sealed class HomeMatcher(
    IAppDbContext db,
    HomeLinker linker,
    IOptions<MatchOptions> matchOptions,
    IOptions<RetentionOptions> retentionOptions,
    TimeProvider clock,
    ILogger<HomeMatcher> logger
)
{
    private const int MaxCandidates = 300;
    private const int MaxPossiblePerListing = 3;

    public MatchSettings Settings =>
        new(
            matchOptions.Value.AutoLinkScore,
            matchOptions.Value.PossibleScore,
            (decimal)matchOptions.Value.SizeTolerance,
            (decimal)matchOptions.Value.LandSizeTolerance
        );

    public async Task<MatchOutcome> MatchAsync(Listing listing, CancellationToken cancellationToken)
    {
        var settings = Settings;
        var scored = await ScoreCandidatesAsync(listing, settings, cancellationToken);
        if (scored.Count == 0)
        {
            return MatchOutcome.None;
        }

        var now = clock.GetUtcNow();
        var best = scored[0];
        if (best.Result.Score >= settings.AutoLinkScore)
        {
            var target = await db
                .Homes.Include(h => h.Bookmark)
                .FirstAsync(h => h.Id == best.HomeId, cancellationToken);
            var targetListings = await db
                .Listings.AsNoTracking()
                .Where(l => l.HomeId == target.Id)
                .Select(l => new { l.DelistedAt })
                .ToListAsync(cancellationToken);
            var allDelisted =
                targetListings.Count > 0 && targetListings.All(l => l.DelistedAt != null);
            var previousPrice = target.CurrentPrice;
            var previousEnded = allDelisted ? targetListings.Max(l => l.DelistedAt) : null;

            db.HomeMatches.Add(HomeMatch.AutoLinked(listing.Id, target.Id, best.Result, now));
            await linker.MoveListingAsync(listing, target, cancellationToken);
            logger.LogInformation(
                "Auto-linked Listing {ListingId} to Home {HomeId} (score {Score})",
                listing.Id,
                target.Id,
                best.Result.Score
            );
            return new MatchOutcome(
                target,
                allDelisted ? HomeListingRole.Repost : HomeListingRole.Duplicate,
                previousPrice,
                previousEnded,
                0
            );
        }

        var possible = scored
            .Where(s => s.Result.Score >= settings.PossibleScore)
            .Take(MaxPossiblePerListing)
            .ToList();
        foreach (var candidate in possible)
        {
            db.HomeMatches.Add(
                HomeMatch.Possible(listing.Id, candidate.HomeId, candidate.Result, now)
            );
        }

        if (possible.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return MatchOutcome.None with
        {
            PossibleMatches = possible.Count,
        };
    }

    /// <summary>Best score per candidate Home, highest first.</summary>
    public async Task<IReadOnlyList<(Guid HomeId, MatchResult Result)>> ScoreCandidatesAsync(
        Listing listing,
        MatchSettings settings,
        CancellationToken cancellationToken
    )
    {
        var cutoff = clock.GetUtcNow().AddDays(-retentionOptions.Value.DelistedDays);
        var query = db
            .Listings.AsNoTracking()
            .Where(l => l.HomeId != listing.HomeId && l.Id != listing.Id)
            .Where(l => l.DelistedAt == null || l.DelistedAt >= cutoff);

        if (listing.PropertyType is { } type)
        {
            query = query.Where(l => l.PropertyType == null || l.PropertyType == type);
        }

        if (listing.RoomCount is decimal rooms)
        {
            query = query.Where(l => l.RoomCount == null || l.RoomCount == rooms);
        }

        if (listing.SizeM2 is decimal size)
        {
            var min = size * (1 - settings.SizeTolerance);
            var max = size * (1 + settings.SizeTolerance);
            query = query.Where(l => l.SizeM2 == null || (l.SizeM2 >= min && l.SizeM2 <= max));
        }
        else if (listing.ImageHash is null)
        {
            // Without size or photo there is too little to go on.
            return [];
        }

        var rejected = await db
            .HomeMatches.AsNoTracking()
            .Where(m => m.ListingId == listing.Id && m.State == HomeMatchState.Rejected)
            .Select(m => m.HomeId)
            .ToListAsync(cancellationToken);

        var candidates = await query
            .OrderByDescending(l => l.LastSeenAt)
            .Take(MaxCandidates)
            .Select(l => new
            {
                l.HomeId,
                Candidate = new MatchCandidate(
                    l.PropertyType,
                    l.RoomCount,
                    l.SizeM2,
                    l.LandSizeM2,
                    l.Location,
                    l.YearBuilt,
                    l.FloorText,
                    l.ImageHash,
                    l.NormalizedTitle,
                    l.NormalizedDescription
                ),
            })
            .ToListAsync(cancellationToken);

        var self = listing.ToMatchCandidate();
        return candidates
            .Where(c => !rejected.Contains(c.HomeId))
            .Select(c => (c.HomeId, Result: HomeMatchScorer.Score(self, c.Candidate, settings)))
            .Where(x => x.Result is not null)
            .GroupBy(x => x.HomeId)
            .Select(g => (HomeId: g.Key, Result: g.MaxBy(x => x.Result!.Score).Result!))
            .OrderByDescending(x => x.Result.Score)
            .ToList();
    }
}
