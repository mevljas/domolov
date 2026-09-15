# Domolov

Self-hosted listing hunter that watches real-estate search URLs, records prices, and notifies the operator of new finds and price changes.

## Language

**Watch**:
A saved crawl target with a provider, search URL, schedule, and notification routes.
_Avoid_: Search, job, alert configuration

**ScanRun**:
One execution of a Watch, including status, timings, counts, and error summary.
_Avoid_: Job run, crawl session (prefer ScanRun in persistence)

**Listing**:
A provider-scoped property advertisement identified by an external id.
_Avoid_: Ad, property, result item

**Location**:
The place line on a Listing (neighborhood or area as shown by the provider).
_Avoid_: Locality, address, area

**LandSizeText**:
Scraped land or plot size for a Listing, stored as free text like SizeText.
_Avoid_: Plot size, land area, parcel size

**PriceObservation**:
A point-in-time price recorded for a Listing.
_Avoid_: Price history entry, price point

**WatchSighting**:
The association of a Listing observed under a specific Watch, with first and last seen times.
_Avoid_: Watch listing link

**Bookmark**:
An operator favorite pointing at a Listing.
_Avoid_: Saved item, favorite (UI may say favorite; domain term is Bookmark)

**NotificationRoute**:
A per-Watch delivery rule that selects channel, destination, and trigger flags.
_Avoid_: Notification, alert rule (when referring to the persisted route)

**ListingProvider**:
A pluggable site adapter that crawls a search URL and yields listing cards.
_Avoid_: Scraper, spider, integration (when referring to the contract)

**CloudflareChallenge**:
A transient provider interstitial that may clear without a human.
_Avoid_: CF protection, CAPTCHA (unless a human puzzle)

**CloudflareBlock**:
A Cloudflare barrier that did not clear within Domolov’s wait; stops the ScanRun.
_Avoid_: Cloudflare challenge (when the wait has already failed)

**CloudflareStrike**:
A per-Watch backoff counter applied after a CloudflareBlock.
_Avoid_: Ban, rate limit (when referring to Domolov’s own backoff)
