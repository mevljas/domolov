using Domolov.Domain.Scans;

namespace Domolov.Domain.Providers;

/// <summary>Normalized listing card produced by a ListingProvider.</summary>
public sealed record ListingCard(
    string ExternalId,
    string Url,
    string Title,
    decimal? Price,
    string Currency,
    string? ImageUrl,
    string? Description,
    string? PropertyType,
    string? Rooms,
    string? SizeText,
    string? YearText,
    string? FloorText,
    string? Location = null,
    string? LandSizeText = null,
    int PageIndex = 1,
    long? ImageHash = null
);

/// <summary>What a crawl reports back besides the cards it yields.</summary>
public sealed class CrawlProgress
{
    /// <summary>
    /// True when the crawl reached the last results page (an empty page), rather than stopping
    /// at the page cap or on a page without a results list. Only such scans count toward delisting.
    /// </summary>
    public bool ReachedEnd { get; set; }

    public int PagesFetched { get; set; }
}

/// <summary>Diagnostic capture from a crawl (e.g. a screenshot of a Cloudflare challenge).</summary>
public sealed record CrawlArtifact(
    ScanArtifactKind Kind,
    string Label,
    string ContentType,
    byte[] Content
);

/// <summary>Input for a provider crawl.</summary>
public sealed record CrawlRequest(Uri SearchUrl, Guid ScanRunId)
{
    public CrawlProgress Progress { get; } = new();

    public Func<CrawlArtifact, CancellationToken, Task>? OnArtifact { get; init; }
}

/// <summary>Pluggable site adapter.</summary>
public interface IListingProvider
{
    string Id { get; }
    bool CanHandle(Uri searchUrl);
    IAsyncEnumerable<ListingCard> CrawlAsync(
        CrawlRequest request,
        CancellationToken cancellationToken
    );
}

/// <summary>Raised when a Cloudflare barrier did not clear within the wait (CloudflareBlock).</summary>
public sealed class CloudflareBlockedException : Exception
{
    public CloudflareBlockedException(string message)
        : base(message) { }

    public CloudflareBlockedException(string message, Exception inner)
        : base(message, inner) { }
}
