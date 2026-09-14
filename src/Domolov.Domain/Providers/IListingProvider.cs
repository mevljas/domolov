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
    int PageIndex = 1
);

/// <summary>Input for a provider crawl.</summary>
public sealed record CrawlRequest(Uri SearchUrl, Guid ScanRunId);

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

/// <summary>Raised when a Cloudflare (or similar) challenge page is detected.</summary>
public sealed class CloudflareBlockedException : Exception
{
    public CloudflareBlockedException(string message)
        : base(message) { }

    public CloudflareBlockedException(string message, Exception inner)
        : base(message, inner) { }
}
