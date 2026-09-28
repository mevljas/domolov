using Domolov.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Domolov.Infrastructure.Scanning;

/// <summary>
/// Shared persistent Chromium context. Full Chromium in new headless mode (not the headless
/// shell), with a fingerprint that stays the same across runs: UA and client hints from the real
/// browser version, configured timezone, Slovenian locale and a common desktop screen.
/// </summary>
public sealed class PlaywrightBrowserHost(
    IOptions<DomolovOptions> options,
    TimeProvider clock,
    ILogger<PlaywrightBrowserHost> logger
) : IAsyncDisposable
{
    private const long DiskCacheBytes = 100L * 1024 * 1024;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowserContext? _context;
    private Dictionary<string, object>? _userAgentOverride;

    /// <summary>Last launch failure, surfaced by the worker health check.</summary>
    public (DateTimeOffset At, string Message)? LastLaunchError { get; private set; }

    public BrowserFingerprint? Fingerprint { get; private set; }

    public string? BrowserVersion { get; private set; }

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

            try
            {
                _context = await LaunchAsync();
                LastLaunchError = null;
                return _context;
            }
            catch (Exception ex)
            {
                LastLaunchError = (clock.GetUtcNow(), ex.Message);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Opens a page with the UA and client-hint override applied before any navigation.</summary>
    public async Task<IPage> NewPageAsync(CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(cancellationToken);
        var page = await context.NewPageAsync();
        if (_userAgentOverride is not null)
        {
            var cdp = await context.NewCDPSessionAsync(page);
            await cdp.SendAsync("Emulation.setUserAgentOverride", _userAgentOverride);
        }

        return page;
    }

    private async Task<IBrowserContext> LaunchAsync()
    {
        _playwright ??= await Playwright.CreateAsync();
        var o = options.Value;
        var userDataDir = o.BrowserUserDataDir;
        Directory.CreateDirectory(userDataDir);
        if (ChromiumProfileLock.TryClearStale(userDataDir))
        {
            logger.LogInformation(
                "Cleared stale Chromium profile lock under {UserDataDir}",
                userDataDir
            );
        }

        var fingerprint = BrowserFingerprintStore.LoadOrCreate(
            userDataDir,
            Random.Shared,
            clock.GetUtcNow()
        );
        Fingerprint = fingerprint;
        var launchOptions = new BrowserTypeLaunchPersistentContextOptions
        {
            Channel = "chromium",
            Headless = o.BrowserHeadless,
            Locale = BrowserFingerprint.Locale,
            TimezoneId = o.TimeZone,
            ViewportSize = new ViewportSize
            {
                Width = fingerprint.ViewportWidth,
                Height = fingerprint.ViewportHeight,
            },
            ScreenSize = new ScreenSize
            {
                Width = fingerprint.ScreenWidth,
                Height = fingerprint.ScreenHeight,
            },
            DeviceScaleFactor = (float)fingerprint.DeviceScaleFactor,
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Accept-Language"] = BrowserFingerprint.AcceptLanguage,
            },
            Args =
            [
                "--disable-blink-features=AutomationControlled",
                $"--disk-cache-size={DiskCacheBytes}",
                "--disable-crash-reporter",
                "--disable-breakpad",
                $"--window-size={fingerprint.ScreenWidth},{fingerprint.ScreenHeight}",
            ],
        };

        logger.LogInformation(
            "Launching Chromium (headless={Headless}, screen {Width}x{Height}, tz {TimeZone})",
            o.BrowserHeadless,
            fingerprint.ScreenWidth,
            fingerprint.ScreenHeight,
            o.TimeZone
        );

        IBrowserContext context;
        try
        {
            context = await _playwright.Chromium.LaunchPersistentContextAsync(
                userDataDir,
                launchOptions
            );
        }
        catch (Exception ex) when (ChromiumProfileLock.LooksLikeProfileInUse(ex))
        {
            logger.LogWarning(ex, "Chromium profile lock conflict; clearing and retrying once");
            ChromiumProfileLock.ForceClear(userDataDir);
            context = await _playwright.Chromium.LaunchPersistentContextAsync(
                userDataDir,
                launchOptions
            );
        }

        await ConfigureUserAgentAsync(context);
        return context;
    }

    private async Task ConfigureUserAgentAsync(IBrowserContext context)
    {
        var probe = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        var cdp = await context.NewCDPSessionAsync(probe);
        var version = await cdp.SendAsync("Browser.getVersion");
        var product = version?.GetProperty("product").GetString() ?? "";
        BrowserVersion = UserAgentBuilder.ParseVersion(product);
        if (BrowserVersion is null)
        {
            logger.LogWarning(
                "Could not read the browser version from {Product}; keeping the default UA",
                product
            );
            return;
        }

        _userAgentOverride = UserAgentBuilder.OverrideArguments(BrowserVersion);
        await cdp.SendAsync("Emulation.setUserAgentOverride", _userAgentOverride);
        logger.LogInformation("Presenting as Chrome {Version} on Linux", BrowserVersion);
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
        _lock.Dispose();
    }
}
