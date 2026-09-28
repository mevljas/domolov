# Full Chromium in new headless mode, a consistent fingerprint, and human pacing

## Status

Accepted. Supersedes [0003](0003-headless-prod-image.md); refines the pacing rule of [0001](0001-cloudflare-challenge-wait.md).

## Context

The production image used `chromium-headless-shell` to save space. The shell reports `HeadlessChrome` in its client hints while Domolov overrode only the UA string, so the two disagreed; the timezone was UTC while the locale was Slovenian; an init script redefined `navigator.webdriver` in a detectable way; and every results page was a direct URL jump with no referrer, 1–3 s apart. Each of these is a stronger bot signal than no custom UA at all. With the worker in its own image (ADR 0005), the size argument no longer applies to the API.

We still do not use CAPTCHA solvers, proxy rotation or stealth patch packs.

## Decision

- The worker runs full Chromium in new headless mode (Playwright channel `chromium`); headed Chromium under Xvfb stays available in `Dockerfile.dev`.
- A `BrowserFingerprint` (screen, viewport, device scale factor) is generated once and persisted next to the browser profile. The UA is built from the real browser version, and matching client hints are applied through CDP `Emulation.setUserAgentOverride`. Timezone comes from `DOMOLOV_TIMEZONE`; locale and `Accept-Language` are Slovenian. The webdriver init script is removed. Chromium is launched with `--disable-blink-features=AutomationControlled` so the real browser's automation bit matches a normal Chrome. That flag is not a stealth pack: there is still no CAPTCHA solver, proxy rotation, stealth plugin, or `navigator.webdriver` override.
- Each scan arrives through the homepage, opens the search with a same-site referrer, and clicks the real next-page link (falling back to a direct URL with a referrer).
- After the page is ready (DOM loaded, results visible), a `HumanPacer` scrolls with the mouse wheel in variable steps, moves the mouse occasionally, and dwells 5–15 s per page (log-normal, clamped). Delays never replace readiness waits.
- Scheduled scans start at a deterministic offset of up to 20 % of the cron interval, seeded by Watch and occurrence, so the next run time shown in the UI stays exact.
- Every CloudflareChallenge and CloudflareBlock stores a screenshot, the HTML and the response headers as ScanRun artifacts; a `browser-check` command prints the presented fingerprint.

## Consequences

- The worker image is larger than the old headless-shell image; the API image is much smaller.
- Scans take longer (roughly 8 s per page plus navigation), which is the point.
- Fingerprint drift is visible through `browser-check` and artifacts instead of guesswork.
