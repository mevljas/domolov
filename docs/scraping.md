# How Domolov browses

Domolov reads public search results the way a person with a browser would, at a pace a person would. This page explains what it does, what it deliberately does not do, and how to diagnose Cloudflare trouble. Decisions are recorded in [ADR 0001](adr/0001-cloudflare-challenge-wait.md) and [ADR 0006](adr/0006-new-headless-chromium-and-human-pacing.md).

## What it does

**One consistent device.** On first start the worker picks a common desktop screen (for example 1536×864) and saves it as `domolov-fingerprint.json` in the browser profile directory. Every scan presents the same screen, viewport and pixel ratio. The browser is full Chromium in new headless mode. Its user agent is built from the real browser version, and the client hints (`sec-ch-ua`, `navigator.userAgentData`) are set to match it. The timezone comes from `DOMOLOV_TIMEZONE`, and the language is Slovenian (`Accept-Language: sl-SI,sl;q=0.9,en;q=0.8`). Cookies persist in the profile, so a cleared Cloudflare check stays cleared.

**Human navigation.** Each scan opens the homepage, moves the mouse, then opens your search with the homepage as referrer. On each results page it waits for the page to be ready (DOM loaded, results visible), then scrolls with the mouse wheel in uneven steps (sometimes a little back up), moves the mouse now and then, and reads for 5–15 seconds before clicking the site's own "next page" link.

**Pacing.** At most `DOMOLOV_MAX_CONCURRENT_SCANS` scans run at once (default 1), with a cooldown of `DOMOLOV_SCAN_COOLDOWN_MS` (±25 %) between them. Scheduled scans start at a stable offset of up to 20 % of their interval, so a six-hourly Watch never starts exactly on the hour.

**Backoff.** A challenge that clears within `DOMOLOV_CLOUDFLARE_CHALLENGE_WAIT_MS` is fine. One that does not is a CloudflareBlock: the ScanRun fails and the Watch backs off exponentially (5 minutes, doubling, up to 6 hours). Run now clears the backoff.

## What it does not do

No CAPTCHA solving, no proxy rotation, no stealth plugin packs, no requests to listing detail pages. Keep request volume low: a few Watches every few hours is plenty for a home search.

## Diagnosing Cloudflare problems

1. **Look at the ScanRun.** In the web app, open *Scans* and select a failed run. Every challenge and block stores a screenshot, the HTML and the response headers (look for `cf-ray` and `cf-mitigated: challenge`). Artifacts are kept for `DOMOLOV_RETENTION_ARTIFACT_DAYS` days.
2. **Check the fingerprint** the worker presents:

   ```bash
   docker compose exec worker dotnet Domolov.Api.dll browser-check
   ```

   It prints the UA, client hints, `navigator.webdriver`, screen and window sizes, WebGL renderer, timezone and languages, using a local page (no third-party site is contacted).
3. **Watch the metrics** (when OpenTelemetry is configured): `domolov.cloudflare.challenges` by outcome and `domolov.cloudflare.clear_time`.
4. **Try headed mode** to compare: `docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build` runs a headed worker under Xvfb.
5. **Reset the device** only as a last resort: stop the worker and delete the `browser-profile` volume (you will lose Cloudflare clearance cookies too).

## Tests

Scraping logic is tested without the live site: parsing uses HTML fixtures, pacing and the UA builder are unit-tested with seeded randomness, and full scan pipelines run against the fixture provider (`DOMOLOV_FAKE_PROVIDER=true`).
