# Group Listings into Homes to track reposts and duplicates

## Status

Accepted.

## Context

Agencies often delete an ad and post the same flat again under a new id (hiding its price history), and several agencies list the same flat at once. Treating every ad as independent produced duplicate cards, "new listing" notifications for old flats, and lost history. Bookmarks and dismissals attached to one ad did not follow the flat.

Alternatives considered:

- Links between Listings (repost chains) with Bookmarks copied forward on linking: smaller change, but state would be duplicated and could diverge.
- Matching only on exact text: misses reworded reposts; matching on price is wrong because reposts usually change it.

## Decision

Introduce a **Home** aggregate. Every Listing belongs to exactly one Home. Bookmark, Dismissal and Unseen live on the Home; feeds show one card per Home represented by its Primary Listing.

New Listings are matched against Homes whose Listings are active or were Delisted within the retention window. SQL prefilters candidates by property type, rooms and size (±3 %). A missing type, room count or size does not exclude a candidate: unknown still matches. `HomeMatchScorer` then combines a 64-bit dHash of the main photo (taken from image bytes the browser already downloaded), trigram similarity of normalised title and description, and agreement of attributes (size, land size, rooms, year, Location, floor). Price is ignored. A score of at least 0.85 links automatically, 0.6 to 0.85 creates a Possible match for review, and the operator can link or unlink manually. A rejected pair is never suggested again. Merging keeps the more advanced BookmarkStage, joins notes with a date marker, and stays Dismissed if either Home was.

A Repost fires the `Reposted` trigger with a comparison to the previous ad. A Duplicate is not a new-listing or Reposted event. If it changes the Home's current price, that price change notifies up or down according to the route's triggers; the same price stays silent. Only a lower price marks the Home Unseen.

## Consequences

- Retention purges forget completely (an explicit operator choice), so reposts are recognised only while the earlier ad is still stored: up to `DOMOLOV_RETENTION_DELISTED_DAYS` after delisting, or indefinitely for Bookmarked Homes.
- Scoring is done in C# after a cheap SQL prefilter, so no PostgreSQL extensions are required and the scorer is unit-testable.
- The v1 → v2 migration creates one Home per existing Listing with the same id and moves Bookmarks onto it.
