# Ideas for later

Suggestions that came up while redesigning Domolov and were deliberately left out for now. Roughly ordered by value.

## Scanning

- **Provider-wide Cloudflare circuit breaker.** A block affects the whole IP and browser session, but backoff is per Watch today, so the next Watch can scan straight into the same wall. Pausing every Watch of that provider after a block would be kinder to the site.
- **Quiet hours.** No scheduled scans overnight (for example 01:00–06:00 local); Run now still works.
- **Quick scans.** Stop a scheduled scan at the first results page with nothing new or changed (on newest-first URLs) and do one full crawl per day. Quick scans would not count as Complete ScanRuns for delisting.
- **Configurable page cap** (currently 50) with a lower default.

## Notifications

- **Durable outbox** with retries and a notification history view (what was sent, when, and failures). Sends are fire-and-forget today; failures only show on the ScanRun.
- **Daily digest** instead of one message per event.

## Product

- **Map view** with MapLibre and OpenStreetMap tiles, geocoding Location through Nominatim and caching the result.
- **Insights per Watch**: median price and price per m² over time, new Homes per week, typical time on market.
- **Compare** two or three Homes side by side, and **CSV export** of Bookmarks.
- **Image proxy/cache** so the browser does not hotlink the provider's CDN (privacy, a stricter CSP, and photos that survive delisting).
- **Passkeys** after the first password sign-in; household **multi-user** with per-user Bookmarks.

## Developer experience

- **.NET Aspire AppHost** to start Postgres, API, worker and the Vite dev server with one command.
- **`/api/v1` versioning** once a second client (for example a mobile app) exists.
