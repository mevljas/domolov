using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Domolov.Domain.Providers;
using Domolov.Domain.Providers.Nepremicnine;

namespace Domolov.Infrastructure.Scanning;

/// <summary>
/// Browser-free provider over embedded fixture data, for smoke tests and demo screenshots only
/// (DOMOLOV_FAKE_PROVIDER=true). Each crawl of a URL advances a round, so repeated scans show
/// price changes, a delisting, a Duplicate, a Possible match and finally a Repost.
/// </summary>
public sealed class FixtureListingProvider : IListingProvider
{
    public const string Host = "fixtures.domolov.test";
    public const string ProviderId = "fixture";
    private const int PageSize = 10;

    private static readonly Lazy<IReadOnlyList<FixtureListing>> Fixtures = new(Load);
    private readonly ConcurrentDictionary<string, int> _rounds = new(
        StringComparer.OrdinalIgnoreCase
    );

    public string Id => ProviderId;

    public bool CanHandle(Uri searchUrl) =>
        searchUrl.Host.Equals(Host, StringComparison.OrdinalIgnoreCase);

    public static string PhotoPath(string photoId) => $"/api/demo/photos/{photoId}.svg";

    /// <summary>Deterministic stand-in for a perceptual hash: same photo id, same hash.</summary>
    public static long PhotoHash(string photoId) => PhotoHash(scope: null, photoId);

    /// <summary>
    /// Hash scoped to one search URL so two fixture Watches do not look like the same Home.
    /// </summary>
    public static long PhotoHash(string? scope, string photoId)
    {
        var key = scope is null ? photoId : scope + "\n" + photoId;
        return BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0);
    }

    /// <summary>
    /// Stable per-URL prefix. Fixture external ids are shared catalogue keys; without a prefix,
    /// a second Watch sighting the same id blocks delisting (and therefore reposts) on the first.
    /// </summary>
    public static string ScopeKey(Uri searchUrl)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(searchUrl.AbsoluteUri));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }

    public static string ScopedExternalId(Uri searchUrl, string catalogueId) =>
        $"{ScopeKey(searchUrl)}-{catalogueId}";

    public static IReadOnlyList<ListingCard> CardsForRound(int round) => CardsForRound(round, null);

    public static IReadOnlyList<ListingCard> CardsForRound(int round, string? scope) =>
        Fixtures
            .Value.Where(f =>
                (f.FromRound ?? 1) <= round && (f.UntilRound is null || round <= f.UntilRound)
            )
            .Select((f, i) => ToCard(f, round, (i / PageSize) + 1, scope))
            .ToList();

    public async IAsyncEnumerable<ListingCard> CrawlAsync(
        CrawlRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var round = _rounds.AddOrUpdate(request.SearchUrl.ToString(), 1, (_, r) => r + 1);
        var cards = CardsForRound(round, ScopeKey(request.SearchUrl));
        foreach (var page in cards.GroupBy(c => c.PageIndex))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken);
            foreach (var card in page)
            {
                yield return card;
            }

            request.Progress.PagesFetched = page.Key;
        }

        request.Progress.ReachedEnd = true;
    }

    private static ListingCard ToCard(FixtureListing f, int round, int pageIndex, string? scope)
    {
        var attrs = NepremicnineParsing.ParseCardAttributes(f.Category, f.Description);
        var price =
            f.PriceByRound?.Where(kv =>
                    int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture) <= round
                )
                .OrderBy(kv => int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture))
                .Select(kv => (decimal?)kv.Value)
                .LastOrDefault() ?? f.Price;
        var externalId = scope is null ? f.Id : $"{scope}-{f.Id}";
        return new ListingCard(
            externalId,
            $"https://{Host}/oglasi-prodaja/{f.Id}/",
            f.Title,
            price,
            "EUR",
            f.Photo is null ? null : PhotoPath(f.Photo),
            f.Description,
            attrs.PropertyType,
            attrs.Rooms,
            attrs.SizeText,
            attrs.YearText,
            attrs.FloorText,
            Location: f.Title,
            LandSizeText: attrs.LandSizeText,
            PageIndex: pageIndex,
            ImageHash: f.Photo is null ? null : PhotoHash(scope, f.Photo)
        );
    }

    private static IReadOnlyList<FixtureListing> Load()
    {
        using var stream =
            typeof(FixtureListingProvider).Assembly.GetManifestResourceStream(
                "Domolov.Infrastructure.Scanning.Fixtures.fixture-listings.json"
            ) ?? throw new InvalidOperationException("Fixture listings resource is missing.");
        var file = JsonSerializer.Deserialize<FixtureFile>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );
        return file?.Listings ?? [];
    }

    private sealed record FixtureFile(List<FixtureListing> Listings);

    private sealed record FixtureListing(
        string Id,
        string? Photo,
        string Title,
        string Category,
        string Description,
        decimal Price,
        int? FromRound,
        int? UntilRound,
        Dictionary<string, decimal>? PriceByRound
    );
}
