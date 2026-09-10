using Domolov.Application.Abstractions;
using Domolov.Domain.Providers;
using Domolov.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Domolov.Infrastructure.Providers;

/// <summary>Resolves listing providers by URL.</summary>
public sealed class ListingProviderResolver(IEnumerable<IListingProvider> providers)
    : IListingProviderResolver
{
    public IListingProvider Resolve(Uri searchUrl)
    {
        var match = providers.FirstOrDefault(p => p.CanHandle(searchUrl));
        return match
            ?? throw new InvalidOperationException(
                $"No listing provider can handle URL: {searchUrl}"
            );
    }

    public IListingProvider? TryGetById(string providerId) =>
        providers.FirstOrDefault(p =>
            string.Equals(p.Id, providerId, StringComparison.OrdinalIgnoreCase)
        );
}

/// <summary>Shared Playwright persistent browser context.</summary>
public sealed class PlaywrightBrowserHost(
    IOptions<DomolovOptions> options,
    ILogger<PlaywrightBrowserHost> logger
) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowserContext? _context;

    public async Task<IBrowserContext> GetContextAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_context is not null)
            {
                return _context;
            }

            _playwright ??= await Playwright.CreateAsync();
            Directory.CreateDirectory(options.Value.BrowserUserDataDir);
            logger.LogInformation(
                "Launching Chromium headless={Headless} userData={UserData}",
                options.Value.BrowserHeadless,
                options.Value.BrowserUserDataDir
            );

            _context = await _playwright.Chromium.LaunchPersistentContextAsync(
                options.Value.BrowserUserDataDir,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = options.Value.BrowserHeadless,
                    Locale = "sl-SI",
                    UserAgent =
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36",
                    Args = ["--disable-blink-features=AutomationControlled"],
                    ViewportSize = new ViewportSize { Width = 1365, Height = 900 },
                }
            );
            return _context;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.CloseAsync();
            _context = null;
        }

        _playwright?.Dispose();
        _playwright = null;
    }
}

/// <summary>Nepremicnine.net listing provider using Playwright.</summary>
public sealed class NepremicnineProvider(
    PlaywrightBrowserHost browserHost,
    ILogger<NepremicnineProvider> logger
) : IListingProvider
{
    public string Id => NepremicnineParsing.ProviderId;

    public bool CanHandle(Uri searchUrl) => NepremicnineParsing.IsNepremicnineHost(searchUrl);

    public async IAsyncEnumerable<ListingCard> CrawlAsync(
        CrawlRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var context = await browserHost.GetContextAsync(cancellationToken);
        var page = await context.NewPageAsync();
        try
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pageIndex = 1;
            var emptyPages = 0;

            while (emptyPages < 1 && pageIndex <= 50)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var url = NepremicnineParsing.BuildPageUrl(request.SearchUrl, pageIndex);
                logger.LogInformation("Crawling {Url} (scan {ScanId})", url, request.ScanRunId);

                var response = await page.GotoAsync(
                    url.ToString(),
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }
                );

                var html = await page.ContentAsync();
                if (NepremicnineParsing.LooksLikeCloudflareChallenge(html))
                {
                    throw new CloudflareBlockedException(
                        $"Cloudflare challenge detected while loading {url}"
                    );
                }

                if (response is { Ok: false } && response.Status >= 400)
                {
                    throw new InvalidOperationException(
                        $"HTTP {(int)response.Status} loading {url}"
                    );
                }

                await DismissCookiesAsync(page);
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

                var list = page.Locator(".seznam, #results, [itemtype*='Offer']").First;
                try
                {
                    await list.WaitForAsync(
                        new LocatorWaitForOptions
                        {
                            State = WaitForSelectorState.Visible,
                            Timeout = 30_000,
                        }
                    );
                }
                catch (TimeoutException)
                {
                    logger.LogWarning("Listing list not found on {Url}", url);
                    yield break;
                }

                var cards = page.Locator(
                    "div.property-box, div.seznam div.estate, article.property, div[itemprop='itemListElement']"
                );
                var count = await cards.CountAsync();
                if (count == 0)
                {
                    cards = page.Locator(".seznam > div, .property-list > div");
                    count = await cards.CountAsync();
                }

                var foundOnPage = 0;
                for (var i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ListingCard? parsed = null;
                    try
                    {
                        parsed = await ParseCardAsync(cards.Nth(i));
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed to parse card {Index}", i);
                    }

                    if (parsed is null || !seen.Add(parsed.ExternalId))
                    {
                        continue;
                    }

                    foundOnPage++;
                    yield return parsed;
                }

                if (foundOnPage == 0)
                {
                    emptyPages++;
                }
                else
                {
                    emptyPages = 0;
                }

                pageIndex++;
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static async Task DismissCookiesAsync(IPage page)
    {
        foreach (var name in new[] { "Zavrni", "Reject", "Decline", "Strinjam se", "Accept" })
        {
            var button = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name });
            if (await button.CountAsync() > 0)
            {
                try
                {
                    await button.First.ClickAsync(new LocatorClickOptions { Timeout = 2_000 });
                }
                catch
                {
                    // ignore
                }

                return;
            }
        }
    }

    private static async Task<ListingCard?> ParseCardAsync(ILocator card)
    {
        var link = card.Locator("a[href*='/oglasi-']").First;
        if (await link.CountAsync() == 0)
        {
            return null;
        }

        var href = await link.GetAttributeAsync("href");
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        if (!href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            href = new Uri(new Uri("https://www.nepremicnine.net"), href).ToString();
        }

        var externalId = NepremicnineParsing.TryExtractExternalId(href);
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return null;
        }

        var title = "Listing";
        var titleLoc = card.Locator("h2, h3, .title, [itemprop='name']").First;
        if (await titleLoc.CountAsync() > 0)
        {
            title = (await titleLoc.InnerTextAsync()).Trim();
        }

        string? image = null;
        var img = card.Locator("img").First;
        if (await img.CountAsync() > 0)
        {
            image = await img.GetAttributeAsync("data-src") ?? await img.GetAttributeAsync("src");
        }

        decimal? price = null;
        var priceMeta = card.Locator("meta[itemprop='price']").First;
        if (await priceMeta.CountAsync() > 0)
        {
            var content = await priceMeta.GetAttributeAsync("content");
            if (NepremicnineParsing.TryParsePrice(content, out var amount))
            {
                price = amount;
            }
        }
        else
        {
            var priceLoc = card.Locator(".price, [class*='price']").First;
            if (await priceLoc.CountAsync() > 0)
            {
                var priceText = await priceLoc.InnerTextAsync();
                if (NepremicnineParsing.TryParsePrice(priceText, out var amount))
                {
                    price = amount;
                }
            }
        }

        string? description = null;
        var descLoc = card.Locator(".kratek, .description, p").First;
        if (await descLoc.CountAsync() > 0)
        {
            description = (await descLoc.InnerTextAsync()).Trim();
        }

        return new ListingCard(
            externalId,
            href,
            string.IsNullOrWhiteSpace(title) ? externalId : title,
            price,
            "EUR",
            image,
            description,
            null,
            null,
            null,
            null,
            null
        );
    }
}
