using Domolov.Application.Abstractions;
using Domolov.Domain.Providers;

namespace Domolov.Infrastructure.Scanning;

/// <summary>Resolves listing providers by URL.</summary>
public sealed class ListingProviderResolver(IEnumerable<IListingProvider> providers)
    : IListingProviderResolver
{
    public IListingProvider Resolve(Uri searchUrl) =>
        providers.FirstOrDefault(p => p.CanHandle(searchUrl))
        ?? throw new InvalidOperationException($"No listing provider can handle URL: {searchUrl}");

    public IListingProvider? TryGetById(string providerId) =>
        providers.FirstOrDefault(p =>
            string.Equals(p.Id, providerId, StringComparison.OrdinalIgnoreCase)
        );

    public bool CanHandle(Uri searchUrl) => providers.Any(p => p.CanHandle(searchUrl));
}
