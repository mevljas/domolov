using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Domolov.Application.Common;
using Domolov.Application.Options;
using Domolov.Domain.Providers;
using Domolov.Domain.Providers.Nepremicnine;
using Domolov.Domain.Scans;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Domolov.Infrastructure.Scanning;

/// <summary>
/// nepremicnine.net provider. Browses like a person: arrives via the homepage, opens the search
/// with a same-site referrer, scrolls and reads each results page, then clicks the real "next"
/// link. Photo hashes come from image responses the browser already downloaded.
/// </summary>
public sealed class NepremicnineProvider(
    PlaywrightBrowserHost browserHost,
    HumanPacer pacer,
    IOptions<DomolovOptions> options,
    ILogger<NepremicnineProvider> logger
) : IListingProvider
{
    private const string OriginUrl = "https://www.nepremicnine.net/";
    private const int MaxCapturedImages = 400;
    private const string ResultsSelector = ".seznam, #results, [itemtype*='Offer']";
    private const string CardSelector =
        "div.property-box, div.seznam div.estate, article.property, div[itemprop='itemListElement']";
    private const string NextLinkSelector =
        "a[rel='next'], .paging a.next, .pagination a.next, a.next, a[title*='Naslednja'], a:has-text('Naslednja')";

    public string Id => NepremicnineParsing.ProviderId;

    public bool CanHandle(Uri searchUrl) => NepremicnineParsing.IsNepremicnineHost(searchUrl);

    public async IAsyncEnumerable<ListingCard> CrawlAsync(
        CrawlRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var page = await browserHost.NewPageAsync(cancellationToken);
        var images = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        page.Response += (_, response) => CaptureImage(response, images);
        try
        {
            await ArriveViaHomepageAsync(page, request, cancellationToken);

            var url = request.SearchUrl;
            var response = await page.GotoAsync(
                url.ToString(),
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Referer = OriginUrl,
                }
            );
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pageIndex = 1;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogInformation("Reading {Url} (scan {ScanId})", page.Url, request.ScanRunId);
                await EnsureClearedAsync(
                    page,
                    response,
                    request,
                    $"page-{pageIndex}",
                    cancellationToken
                );
                if (response is { Ok: false } && response.Status >= 400)
                {
                    throw new InvalidOperationException(
                        $"HTTP {response.Status} loading {page.Url}"
                    );
                }

                await DismissCookiesAsync(page);
                if (!await WaitForResultsAsync(page))
                {
                    logger.LogWarning("Listing list not found on {Url}", page.Url);
                    yield break;
                }

                await BrowseResultsAsync(page, cancellationToken);
                var cards = await ParseCardsAsync(page, pageIndex, images, seen);
                request.Progress.PagesFetched = pageIndex;
                if (cards.Count == 0)
                {
                    request.Progress.ReachedEnd = true;
                    yield break;
                }

                foreach (var card in cards)
                {
                    yield return card;
                }

                if (pageIndex >= CrawlPagination.DefaultMaxPages)
                {
                    logger.LogWarning(
                        "Stopped at the page cap ({Max}) for {Url}",
                        CrawlPagination.DefaultMaxPages,
                        url
                    );
                    yield break;
                }

                pageIndex++;
                response = await GoToNextPageAsync(
                    page,
                    request.SearchUrl,
                    pageIndex,
                    cancellationToken
                );
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task ArriveViaHomepageAsync(
        IPage page,
        CrawlRequest request,
        CancellationToken cancellationToken
    )
    {
        var response = await page.GotoAsync(
            OriginUrl,
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }
        );
        await EnsureClearedAsync(page, response, request, "homepage", cancellationToken);
        await DismissCookiesAsync(page);
        await page.WaitForLoadStateAsync(
                LoadState.Load,
                new PageWaitForLoadStateOptions { Timeout = 15_000 }
            )
            .ContinueWith(_ => { }, TaskScheduler.Default);
        await MoveMouseIdlyAsync(page, cancellationToken);
        await pacer.DelayAsync(pacer.ShortPause() * 3, cancellationToken);
    }

    private async Task<IResponse?> GoToNextPageAsync(
        IPage page,
        Uri searchUrl,
        int pageIndex,
        CancellationToken cancellationToken
    )
    {
        var next = page.Locator(NextLinkSelector).First;
        if (await next.CountAsync() > 0 && await next.IsVisibleAsync())
        {
            try
            {
                await next.ScrollIntoViewIfNeededAsync();
                await next.HoverAsync(new LocatorHoverOptions { Timeout = 5_000 });
                await pacer.DelayAsync(pacer.ShortPause(), cancellationToken);
                var response = await page.RunAndWaitForResponseAsync(
                    () => next.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }),
                    r => r.Request.IsNavigationRequest && r.Frame == page.MainFrame,
                    new PageRunAndWaitForResponseOptions { Timeout = 60_000 }
                );
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                return response;
            }
            catch (TimeoutException ex)
            {
                logger.LogDebug(ex, "Clicking the next-page link timed out; navigating directly");
            }
        }

        var fallback = NepremicnineParsing.BuildPageUrl(searchUrl, pageIndex);
        return await page.GotoAsync(
            fallback.ToString(),
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Referer = page.Url }
        );
    }

    private static async Task<bool> WaitForResultsAsync(IPage page)
    {
        try
        {
            await page.Locator(ResultsSelector)
                .First.WaitForAsync(
                    new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = 30_000,
                    }
                );
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Scrolls through the results with the mouse wheel and reads for a while.</summary>
    private async Task BrowseResultsAsync(IPage page, CancellationToken cancellationToken)
    {
        var dwell = pacer.DwellTime();
        var started = Stopwatch.GetTimestamp();
        var height = await page.EvaluateAsync<int>(
            "() => document.documentElement.scrollHeight - window.innerHeight"
        );
        foreach (var step in pacer.ScrollPlan(Math.Max(0, height)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await page.Mouse.WheelAsync(0, step);
            await pacer.DelayAsync(pacer.ShortPause(), cancellationToken);
            if (pacer.Chance(0.15))
            {
                await MoveMouseIdlyAsync(page, cancellationToken);
            }

            if (Stopwatch.GetElapsedTime(started) > dwell * 2)
            {
                break;
            }
        }

        await pacer.DelayAsync(dwell - Stopwatch.GetElapsedTime(started), cancellationToken);
    }

    private async Task MoveMouseIdlyAsync(IPage page, CancellationToken cancellationToken)
    {
        var viewport =
            page.ViewportSize ?? new PageViewportSizeResult { Width = 1366, Height = 768 };
        var (x, y, steps) = pacer.MouseTarget(viewport.Width, viewport.Height);
        await page.Mouse.MoveAsync(x, y, new MouseMoveOptions { Steps = steps });
        await pacer.DelayAsync(pacer.ShortPause(), cancellationToken);
    }

    private async Task EnsureClearedAsync(
        IPage page,
        IResponse? response,
        CrawlRequest request,
        string label,
        CancellationToken cancellationToken
    )
    {
        var html = await page.ContentAsync();
        if (!NepremicnineParsing.LooksLikeCloudflareChallenge(html))
        {
            return;
        }

        await CaptureArtifactsAsync(
            page,
            response,
            request,
            $"challenge-{label}",
            cancellationToken
        );
        var waitMs = options.Value.CloudflareChallengeWaitMs;
        logger.LogInformation(
            "CloudflareChallenge on {Label}; waiting up to {WaitMs}ms",
            label,
            waitMs
        );
        var started = Stopwatch.GetTimestamp();
        try
        {
            await CloudflareChallengeGate.WaitUntilClearedOrThrowAsync(
                async _ => await page.ContentAsync(),
                waitMs,
                $"CloudflareBlock while loading {page.Url}",
                cancellationToken
            );
            DomolovTelemetry.CloudflareChallenges.Add(
                1,
                new KeyValuePair<string, object?>("outcome", "cleared")
            );
            DomolovTelemetry.CloudflareClearTime.Record(
                Stopwatch.GetElapsedTime(started).TotalSeconds
            );
        }
        catch (CloudflareBlockedException)
        {
            DomolovTelemetry.CloudflareChallenges.Add(
                1,
                new KeyValuePair<string, object?>("outcome", "blocked")
            );
            await CaptureArtifactsAsync(
                page,
                response,
                request,
                $"block-{label}",
                cancellationToken
            );
            throw;
        }
    }

    private async Task CaptureArtifactsAsync(
        IPage page,
        IResponse? response,
        CrawlRequest request,
        string label,
        CancellationToken cancellationToken
    )
    {
        if (request.OnArtifact is null)
        {
            return;
        }

        try
        {
            var screenshot = await page.ScreenshotAsync(
                new PageScreenshotOptions { Type = ScreenshotType.Png }
            );
            await request.OnArtifact(
                new CrawlArtifact(ScanArtifactKind.Screenshot, label, "image/png", screenshot),
                cancellationToken
            );
            var html = Encoding.UTF8.GetBytes(await page.ContentAsync());
            await request.OnArtifact(
                new CrawlArtifact(ScanArtifactKind.Html, label, "text/html; charset=utf-8", html),
                cancellationToken
            );
            if (response is not null)
            {
                var headers = new StringBuilder()
                    .Append(response.Status)
                    .Append(' ')
                    .AppendLine(response.Url);
                foreach (var (name, value) in await response.AllHeadersAsync())
                {
                    headers.Append(name).Append(": ").AppendLine(value);
                }

                await request.OnArtifact(
                    new CrawlArtifact(
                        ScanArtifactKind.Headers,
                        label,
                        "text/plain; charset=utf-8",
                        Encoding.UTF8.GetBytes(headers.ToString())
                    ),
                    cancellationToken
                );
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Could not capture artifacts for {Label}", label);
        }
    }

    private static void CaptureImage(
        IResponse response,
        ConcurrentDictionary<string, byte[]> images
    )
    {
        if (
            images.Count >= MaxCapturedImages
            || response.Request.ResourceType != "image"
            || !response.Url.Contains("nepremicnine.net", StringComparison.OrdinalIgnoreCase)
            || !response.Ok
        )
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                images[NormalizeImageUrl(response.Url)] = await response.BodyAsync();
            }
            catch (PlaywrightException)
            {
                // Body unavailable (e.g. page closed); the card simply gets no photo hash.
            }
        });
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
                catch (PlaywrightException)
                {
                    // Banner may have closed on its own.
                }

                return;
            }
        }
    }

    private async Task<List<ListingCard>> ParseCardsAsync(
        IPage page,
        int pageIndex,
        ConcurrentDictionary<string, byte[]> images,
        HashSet<string> seen
    )
    {
        var cards = page.Locator(CardSelector);
        var count = await cards.CountAsync();
        if (count == 0)
        {
            cards = page.Locator(".seznam > div, .property-list > div");
            count = await cards.CountAsync();
        }

        var result = new List<ListingCard>();
        for (var i = 0; i < count; i++)
        {
            try
            {
                var card = await ParseCardAsync(cards.Nth(i), pageIndex, images);
                if (card is not null && seen.Add(card.ExternalId))
                {
                    result.Add(card);
                }
            }
            catch (PlaywrightException ex)
            {
                logger.LogDebug(ex, "Failed to parse card {Index}", i);
            }
        }

        return result;
    }

    private static async Task<ListingCard?> ParseCardAsync(
        ILocator card,
        int pageIndex,
        ConcurrentDictionary<string, byte[]> images
    )
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

        var title = externalId;
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
            if (image is not null && image.StartsWith("//", StringComparison.Ordinal))
            {
                image = "https:" + image;
            }
        }

        decimal? price = null;
        var priceLoc = card.Locator(".price, [class*='price']").First;
        if (
            await priceLoc.CountAsync() > 0
            && NepremicnineParsing.TryParsePrice(await priceLoc.InnerTextAsync(), out var amount)
        )
        {
            price = amount;
        }

        if (price is null)
        {
            var priceMeta = card.Locator("meta[itemprop='price']").First;
            if (
                await priceMeta.CountAsync() > 0
                && NepremicnineParsing.TryParsePrice(
                    await priceMeta.GetAttributeAsync("content"),
                    out var metaAmount
                )
            )
            {
                price = metaAmount;
            }
        }

        string? description = null;
        var descLoc = card.Locator(".kratek, .description, p").First;
        if (await descLoc.CountAsync() > 0)
        {
            description = (await descLoc.InnerTextAsync()).Trim();
        }

        string? categoryLine = null;
        foreach (
            var line in (await card.InnerTextAsync()).Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            if (
                line.StartsWith("Prodaja:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Oddaja:", StringComparison.OrdinalIgnoreCase)
            )
            {
                categoryLine = line;
                break;
            }
        }

        var attrs = NepremicnineParsing.ParseCardAttributes(categoryLine, description);
        long? imageHash = null;
        if (image is not null && images.TryGetValue(NormalizeImageUrl(image), out var bytes))
        {
            imageHash = ImageHasher.Compute(bytes);
        }

        return new ListingCard(
            externalId,
            href,
            string.IsNullOrWhiteSpace(title) ? externalId : title,
            price,
            "EUR",
            RewriteImageHost(image),
            description,
            attrs.PropertyType,
            attrs.Rooms,
            attrs.SizeText,
            attrs.YearText,
            attrs.FloorText,
            Location: string.IsNullOrWhiteSpace(title) ? null : title,
            LandSizeText: attrs.LandSizeText,
            PageIndex: pageIndex,
            ImageHash: imageHash
        );
    }

    private static string NormalizeImageUrl(string url)
    {
        var withoutQuery = url.Split('?', 2)[0];
        return withoutQuery.Replace(
            "img.onnepremicnine.net",
            "img.nepremicnine.net",
            StringComparison.OrdinalIgnoreCase
        );
    }

    /// <summary>The CDN host that serves images to browsers without the site's referrer checks.</summary>
    private static string? RewriteImageHost(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl)
            ? imageUrl
            : imageUrl.Replace(
                "img.nepremicnine.net",
                "img.onnepremicnine.net",
                StringComparison.OrdinalIgnoreCase
            );
}
