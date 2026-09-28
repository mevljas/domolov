using System.Text.Json;
using Microsoft.Playwright;

namespace Domolov.Infrastructure.Scanning;

/// <summary>Reports the fingerprint Domolov's browser presents, using a local page only.</summary>
public sealed class BrowserCheck(PlaywrightBrowserHost host)
{
    private const string Probe = """
        async () => {
          const gl = document.createElement('canvas').getContext('webgl');
          const dbg = gl && gl.getExtension('WEBGL_debug_renderer_info');
          const uaData = navigator.userAgentData
            ? await navigator.userAgentData.getHighEntropyValues(['fullVersionList', 'platformVersion', 'architecture', 'bitness'])
            : null;
          return {
            userAgent: navigator.userAgent,
            userAgentData: uaData,
            webdriver: navigator.webdriver,
            languages: navigator.languages,
            timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
            screen: { width: screen.width, height: screen.height, availWidth: screen.availWidth, availHeight: screen.availHeight },
            window: { innerWidth, innerHeight, outerWidth, outerHeight, devicePixelRatio },
            hardwareConcurrency: navigator.hardwareConcurrency,
            plugins: navigator.plugins.length,
            webgl: dbg ? { vendor: gl.getParameter(dbg.UNMASKED_VENDOR_WEBGL), renderer: gl.getParameter(dbg.UNMASKED_RENDERER_WEBGL) } : null
          };
        }
        """;

    public async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var page = await host.NewPageAsync(cancellationToken);
        try
        {
            await page.SetContentAsync(
                "<!doctype html><html lang='sl'><head><title>check</title></head><body></body></html>"
            );
            var result = await page.EvaluateAsync<JsonElement>(Probe);
            var report = new
            {
                browserVersion = host.BrowserVersion,
                fingerprint = host.Fingerprint,
                page = result,
            };
            return JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
