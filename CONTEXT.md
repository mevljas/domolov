# Domolov

Self-hosted listing hunter that watches real-estate search URLs, records prices, groups ads of the same dwelling, and notifies the operator of new finds, price changes and reposts.

## Language

**Watch**:
A saved crawl target with a provider, search URL, schedule, and notification routes.
_Avoid_: Search, job, alert configuration

**Paused Watch**:
A Watch the scheduler skips. Manual Run now still works; its Listings and routes are kept.
_Avoid_: Disabled, inactive, archived

**ScanRun**:
One execution of a Watch, including status, timings, counts, and error summary.
_Avoid_: Job run, crawl session

**Complete ScanRun**:
A successful non-baseline ScanRun whose crawl reached the last results page (not stopped by the page cap or a missing results list).
_Avoid_: Full scan

**Interrupted** (ScanRun status):
The process stopped mid-scan; recovered at startup or shutdown so the Watch can scan again.

**Home**:
The real-world dwelling behind one or more Listings. Every Listing belongs to exactly one Home; a new Listing gets its own Home unless it is matched to an existing one.
_Avoid_: Property, estate, unit

**Listing**:
One provider advertisement for a Home, identified by provider and external id.
_Avoid_: Ad (in code), result item

**PropertyType**:
The provider's category text on a Listing (such as Stanovanje or Hiša), kept for display and matching.
_Avoid_: Property (as a synonym for Home), estate type

**Primary Listing**:
The Home's Listing that represents it in feeds: the most recently first seen active Listing, or the last one seen when none are active.

**Repost**:
A new Listing of a Home that appears after every earlier Listing of that Home was Delisted.
_Avoid_: Relisted (that is the same Listing coming back)

**Duplicate Listing**:
Another Listing of the same Home that is active at the same time (another agency, or the same agency twice).

**HomeMatch**:
The scored link between a Listing and a Home, recording which signals matched. States: Auto-linked, Possible (awaiting review), Confirmed, Rejected (different homes; never suggested again).
_Avoid_: Duplicate detection result

**Location**:
The place line on a Listing (neighborhood or area as shown by the provider).
_Avoid_: Locality, address, area

**Size / LandSize / Rooms / YearBuilt**:
Numeric values parsed from the scraped text of a Listing; the text (such as LandSizeText) is kept for display.
_Avoid_: Plot size, parcel size

**PriceObservation**:
A point-in-time price recorded for a Listing.
_Avoid_: Price history entry, price point

**WatchSighting**:
The association of a Listing observed under a specific Watch, with first and last seen times. A sighting made before the Watch's current search URL is stale.
_Avoid_: Watch listing link

**Delisted Listing**:
A Listing missed by two consecutive Complete ScanRuns of every Watch that sighted it under its current search URL.

**Relisted**:
A Delisted Listing (same external id) seen again.

**Off market**:
A Home whose Listings are all Delisted.

**Time on market**:
For a Home, from the first sighting of any of its Listings until it went Off market (or now), with gaps between ads shown.

**Bookmark**:
The operator's tracking record for a Home, with a BookmarkStage and an optional private note.
_Avoid_: Favorite, saved item, shortlist entry

**BookmarkStage**:
Interested, Contacted, Viewing scheduled, Viewed, Offer made, Rejected. A Rejected Bookmark stays on the board and still gets price notifications.

**Dismissed Home**:
A Home hidden from feeds and never notified, including its future Reposts and Duplicates. Dismissing removes its Bookmark; it can be undone.
_Avoid_: Hidden, rejected

**Unseen Home**:
A new Home the operator has not opened or marked seen. A lower price makes it Unseen again; a same-price Repost or Duplicate does not.

**Purge**:
Permanent removal of a Listing with its PriceObservations, WatchSightings and HomeMatches by retention (Delisted, or no longer sighted by any Watch, for longer than the retention period, and its Home is not Bookmarked). A Home is deleted when its last Listing is purged. Nothing is remembered: an ad that reappears after its Home was purged is a new Listing of a new Home, even if the Home had been Dismissed.

**NotificationRoute**:
A per-Watch delivery rule that selects channel, destination, and trigger flags (new listing, price down/up, any price change, reposted).
_Avoid_: Notification, alert rule (when referring to the persisted route)

**ListingProvider**:
A pluggable site adapter that crawls a search URL and yields listing cards.
_Avoid_: Scraper, spider, integration (when referring to the contract)

**CloudflareChallenge**:
A transient provider interstitial that may clear without a human.
_Avoid_: CF protection, CAPTCHA (unless a human puzzle)

**CloudflareBlock**:
A Cloudflare barrier that did not clear within Domolov's wait; stops the ScanRun.
_Avoid_: Cloudflare challenge (when the wait has already failed)

**CloudflareStrike**:
A per-Watch backoff counter applied after a CloudflareBlock.
_Avoid_: Ban, rate limit (when referring to Domolov's own backoff)
