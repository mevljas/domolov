# Upgrading

## From the single Blazor image (pre-SPA)

The old Compose stack ran one `domolov` container (Blazor Server + worker + Chromium) on port 8080. The new stack is three images behind the same port.

| Then | Now |
|---|---|
| `ghcr.io/mevljas/domolov` | `domolov-api`, `domolov-worker`, `domolov-web` |
| `DOMOLOV_ROLE=web\|worker\|all` | `api\|worker\|all` (`web` still works; it means `api`) |
| Cookie tickets in a Data Protection volume | Keys in PostgreSQL — you can drop that volume |
| Blazor UI talking to handlers in-process | Vue SPA on nginx, same-origin `/api` |
| Listings only | Listings grouped into **Homes**, plus Bookmarks, Matches, retention |

### Steps

1. **Back up PostgreSQL.** `docker compose exec postgres pg_dump -U domolov domolov > backup.sql`
2. Pull the new repo revision (or new image tags).
3. Keep the same `POSTGRES_PASSWORD` and the same `DOMOLOV_ADMIN_PASSWORD` / `_HASH`.
4. `docker compose up -d`. The `migrate` container applies `HomesSessionsAndRetention` and backfills a Home for every existing Listing (title, description, photo hash, attributes). Bookmarks on Listings become Bookmarks on those Homes.
5. Sign in. You should see one Home per old Listing. Recreate notification routes if a Watch is missing them (destinations were not rewritten).
6. Remove the leftover `domolov` container/image when you are happy: `docker compose rm -sf domolov` (only if your old compose file still defines that service).

Existing ScanRuns stay. The first scan of each Watch after the upgrade is *not* a new baseline — baseline is “first successful ScanRun ever”, already recorded.

If you still have `DOMOLOV_BROWSER_HEADLESS=false` in a leftover `.env`, the **production** worker will refuse to start. Either set it to `true` or use `docker-compose.dev.yml`. The smoke overlay forces headless so a local headed `.env` cannot break CI or screenshots.

## From any tagged SPA release

1. Read the release notes for env vars and migrations.
2. `git pull` (or bump the three image tags together).
3. `docker compose up -d` — migrate runs before api/worker become healthy.
4. On Kubernetes: bump all three tags in the overlay and `kubectl apply -k`. The api init container migrates before new api pods take traffic.

Do not run a new worker against an old schema, and do not mix tags across api / worker / web.

## Rollback

Restore the database dump taken in step 1 and start the previous images. Forward migrations are not automatically reversible once Homes have been merged or listings purged.

## After upgrade checklist

- [ ] Sign in still works (same password / hash)
- [ ] Each old Watch still lists, with its URL and schedule
- [ ] Homes feed is populated (one Home per previous Listing, plus any the matcher linked)
- [ ] A **Run now** completes (baseline or succeeded, not stuck queued)
- [ ] Notification **Send test** still reaches Discord / Telegram / email / Push
- [ ] `/api/docs` loads after sign-in
