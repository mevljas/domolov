# Domolov

<p align="center">
  <img src="docs/assets/brand/wordmark.svg" alt="Domolov" width="280">
</p>

<p align="center">
  <strong>The listing hunter that remembers the house, not just the ad.</strong>
</p>

<p align="center">
  Self-hosted. Slovenian. Quiet in the background. Loud when the price drops.
</p>

**Domolov** (*dom* = home + *lov* = hunt) watches [nepremicnine.net](https://www.nepremicnine.net/) search URLs for you. It stores every sighting, groups ads that are the same dwelling, and pings you on Discord, Telegram, email or the browser when something new, cheaper or *reposted* appears.

Paste a search. Set a schedule. Go look at actual houses.

---

## What you get

- **Watches, not scrapers.** You copy a real search URL (Slovenian or English — they are different pages). Filters stay in the URL. Domolov does not rewrite them.
- **Homes, not a pile of ads.** The same flat listed twice, or deleted and put back with a new id, becomes one **Home**. Photos are compared with a perceptual hash, text with trigrams, attributes with a scorer. You review the close calls.
- **A silent first scan.** The first successful **ScanRun** is a **baseline**: listings are stored, nothing is sent. After that, only what you asked for.
- **Price history that spans ads.** A Home keeps every price point across Original, Repost and Duplicate listings, plus time on market and the gaps when it was off the market.
- **Bookmarks with a pipeline.** Six stages (Interested → Contacted → Viewing scheduled → Viewed → Offer made → Rejected) and a private note. A bookmarked Home is never purged.
- **Dismiss, for the ones you never want again.** Hidden from feeds, never notified, including future reposts. Undo is one click.
- **Notifications you control.** Per Watch: Discord webhook, Telegram chat, email, or Web Push. Triggers: new listing, price down, price up, any price change, reposted. **Send test** before you trust it.
- **A browser that looks like a browser.** Full Chromium in new headless mode, a persisted fingerprint, real client hints, mouse-wheel scrolling and 5–15 s dwell. Schedule jitter so nothing fires on the hour. See [how it browses](docs/scraping.md).
- **Storage that does not grow forever.** Daily cleanup purges delisted listings after 30 days (unless bookmarked), trims ScanRuns, drops dead push subscriptions, and caps the browser cache.
- **An installable app.** Vue 3 SPA, Slovenian + English, light and dark, PWA, live scan status over Server-Sent Events.
- **A real API.** REST under `/api`, OpenAPI 3.1, Scalar docs at `/api/docs`, RFC 9457 problems, weak ETags.

More ideas we parked on purpose: [docs/improvements.md](docs/improvements.md).

---

## Look around

Screenshots (light / dark, desktop / phone) land in [`docs/assets/screenshots/`](docs/assets/screenshots/) when you run `pnpm -C frontend docs:screenshots` against the [smoke stack](#try-it-without-touching-the-live-site). Until then, the mark is this:

<p align="center">
  <img src="docs/assets/brand/logo-mark.svg" alt="Domolov mark" width="96">
</p>

| Page | What you do there |
|---|---|
| **Dashboard** | Unseen Homes, price drops, Watch health, live scans |
| **Homes** | Photo-first feed, filters, dismiss with undo |
| **Home** | Timeline of ads, price chart, bookmark + note |
| **Matches** | Side-by-side “same house or not?” |
| **Bookmarks** | Kanban across the six stages |
| **Watches** | Create from a pasted URL, pause, Run now |
| **Scans** | Live table + challenge screenshots |
| **Settings** | Theme, language, Web Push, sessions, storage, danger zone |

---

## Quick start

You need Docker and about two minutes.

```bash
git clone https://github.com/mevljas/domolov.git
cd domolov
cp .env.example .env
# set a real password
```

Edit `.env`:

```dotenv
DOMOLOV_ADMIN_PASSWORD=pick-something-long
POSTGRES_PASSWORD=also-not-the-default
```

Then:

```bash
docker compose up -d
```

Open [http://localhost:8080](http://localhost:8080), sign in, paste a nepremicnine.net results URL, pick a schedule. The first scan is silent. The next one talks.

Prefer a hash over a plaintext password:

```bash
docker compose run --rm api hash-password 'pick-something-long'
# put the printed line in DOMOLOV_ADMIN_PASSWORD_HASH
```

**Put this behind TLS** if it is reachable from the internet. The operator password is a convenience lock, not a perimeter.

Full walkthrough: [Getting started](docs/guides/getting-started.md).

### Try it without touching the live site

The smoke overlay swaps nepremicnine.net for canned listings (including a deliberate repost):

```bash
docker compose -f docker-compose.yml -f docker-compose.smoke.yml up -d --wait
# sign in with password: smoke-password
```

Create a Watch whose URL host is `fixtures.domolov.test`. Each **Run now** advances a scripted round: baseline → new + price drop + a possible match → one listing off market → that flat reposted.

---

## How a hunt works

```text
  you paste a search URL
            │
            ▼
         Watch  ── cron (with jitter) or Run now
            │
            ▼
        ScanRun  ── Chromium walks the results pages
            │
            ▼
        Listing  ── one provider ad, with prices and a photo hash
            │
            ▼
          Home   ── the dwelling; Original / Repost / Duplicate ads hang off it
            │
            ├── Unseen? → dashboard + feed
            ├── match score in the grey zone? → Matches queue
            └── triggers fire? → Discord / Telegram / email / Web Push
```

**Watch.** A saved crawl target: provider, search URL, schedule, optional pause, notification routes. Pausing skips the scheduler; **Run now** still works.

**ScanRun.** One execution. Statuses: queued, running, baseline, succeeded, failed, interrupted. The first success is baseline (no notifications). A run that reached the last results page is a **Complete ScanRun** — those are what delisting counts.

**Listing.** One ad on the site, keyed by provider + external id. Price history lives here. Missed by two consecutive Complete ScanRuns of every Watch that saw it → **Delisted**.

**Home.** The real-world dwelling. Every Listing belongs to exactly one. A new ad is its own Home unless the matcher links it. You can confirm, reject, or unlink by hand.

**Off market.** Every Listing of the Home is Delisted. Time on market still shows, including the gaps between ads.

Words we use on purpose (and the ones we do not): [CONTEXT.md](CONTEXT.md).

---

## Notifications

Server-wide credentials live in env. Each Watch has **NotificationRoutes**: channel + destination + triggers.

| Channel | Destination | Server env |
|---|---|---|
| Discord | Incoming webhook URL | none (URL is the secret) |
| Telegram | Chat id | `DOMOLOV_TELEGRAM_BOT_TOKEN` |
| Email | To address | `DOMOLOV_SMTP_*` |
| Web Push | `all` (every subscribed browser) | `DOMOLOV_VAPID_*` |

Triggers: **new listing**, **price decreased**, **price increased**, **any price change**, **reposted**.

Set `DOMOLOV_PUBLIC_URL` (e.g. `https://domolov.example.com`) so messages include an **Open in Domolov** link.

Guides: [Discord](docs/guides/discord.md) · [Telegram](docs/guides/telegram.md) · [Email](docs/guides/email.md) · [Web Push](docs/guides/web-push.md)

---

## Configuration

Everything is env-driven. Compose reads `.env`. Empty values mean *unset* (Compose still passes the key; Domolov ignores blanks).

| Variable | What it does | Default |
|---|---|---|
| `DOMOLOV_ADMIN_PASSWORD` | Operator sign-in | required (or the hash) |
| `DOMOLOV_ADMIN_PASSWORD_HASH` | PBKDF2 hash from `hash-password`; wins if both are set | |
| `DOMOLOV_SESSION_LIFETIME_DAYS` | Sliding cookie lifetime | `14` |
| `ConnectionStrings__Default` | PostgreSQL. Compose builds this from `POSTGRES_PASSWORD` | required outside Compose |
| `DOMOLOV_ROLE` | `api` / `worker` / `all` (`web` is a deprecated alias for `api`) | `all` |
| `DOMOLOV_TIMEZONE` | IANA zone for cron and the daily cleanup | `Europe/Ljubljana` |
| `DOMOLOV_MAX_CONCURRENT_SCANS` | Parallel Playwright ScanRuns | `1` |
| `DOMOLOV_SCAN_COOLDOWN_MS` | Pause after each ScanRun (±25 % jitter) | `20000` |
| `DOMOLOV_CLOUDFLARE_CHALLENGE_WAIT_MS` | How long to wait for a challenge to clear | `30000` |
| `DOMOLOV_BROWSER_HEADLESS` | `true` = new headless (prod image). `false` needs the [dev overlay](#headed-chromium) | `true` |
| `DOMOLOV_BROWSER_USER_DATA_DIR` | Persistent Chromium profile | `/data/browser-profile` |
| `DOMOLOV_FAKE_PROVIDER` | Serve canned listings instead of the live site | `false` |
| `DOMOLOV_PUBLIC_URL` | Public origin of the web app (notification deep links) | |
| `DOMOLOV_MATCH_AUTO_LINK_SCORE` | Auto-link Listings at or above this score (0–1) | `0.85` |
| `DOMOLOV_MATCH_POSSIBLE_SCORE` | Offer a Possible match at or above this score | `0.6` |
| `DOMOLOV_RETENTION_DELISTED_DAYS` | Purge a delisted Listing after this many days, unless bookmarked | `30` |
| `DOMOLOV_RETENTION_ORPHAN_LISTING_DAYS` | Same, for Listings no Watch sights any more | `30` |
| `DOMOLOV_RETENTION_SCAN_RUN_DAYS` | Drop old ScanRuns (always keep the newest *K* per Watch) | `90` |
| `DOMOLOV_RETENTION_SCAN_RUN_KEEP_PER_WATCH` | That *K* | `20` |
| `DOMOLOV_RETENTION_ARTIFACT_DAYS` | Challenge screenshots / HTML / headers | `14` |
| `DOMOLOV_RETENTION_SESSION_DAYS` | Revoked or idle sessions | `30` |
| `DOMOLOV_RETENTION_DAILY_AT` | Local time of the daily cleanup (`HH:mm`) + 0–30 min jitter | `04:00` |
| `DOMOLOV_TELEGRAM_BOT_TOKEN` | Telegram | |
| `DOMOLOV_SMTP_HOST` / `_PORT` / `_USER` / `_PASSWORD` / `_FROM` | SMTP | port `587` |
| `DOMOLOV_VAPID_PUBLIC_KEY` / `_PRIVATE_KEY` / `_SUBJECT` | Web Push | |
| `DOMOLOV_LOG_LEVEL` | Serilog: Verbose … Fatal | `Information` |
| `DOMOLOV_API_UPSTREAM` | Where the web container proxies `/api` and `/health` | `http://api:8080` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Traces and metrics | off when empty |

Full commented list: [`.env.example`](.env.example).

After a container recreate, Domolov clears stale Chromium `Singleton*` locks in the profile directory. If scans still say the profile is in use, delete the `browser-profile` volume and start again (you will also lose Cloudflare clearance cookies).

---

## Local development

Needs the **.NET 10 SDK** (or `./install-dotnet.ps1` / `./install-dotnet.sh`), **Node 24**, **pnpm** via Corepack, and Docker for PostgreSQL.

```bash
docker compose up -d postgres
dotnet restore
dotnet run --project src/Domolov.Api
# in another terminal
corepack enable
pnpm -C frontend install
pnpm -C frontend dev
```

The API listens on [http://localhost:5080](http://localhost:5080). Vite on [http://localhost:5173](http://localhost:5173) proxies `/api` and `/health` there. Sign in with `DOMOLOV_ADMIN_PASSWORD` (default `changeme` if you have not set one).

```bash
ConnectionStrings__Default=Host=localhost;Port=5432;Database=domolov;Username=domolov;Password=domolov
DOMOLOV_ADMIN_PASSWORD=changeme
DOMOLOV_ROLE=all
```

CLI commands on the same binary:

```bash
dotnet run --project src/Domolov.Api -- migrate
dotnet run --project src/Domolov.Api -- hash-password 'secret'
dotnet run --project src/Domolov.Api -- browser-check
```

### Headed Chromium

The published worker image is headless-only. To watch the browser under Xvfb:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

### Formatting and tests

```bash
dotnet tool restore
dotnet csharpier format .
dotnet csharpier check .
dotnet test

pnpm -C frontend lint
pnpm -C frontend format:check
pnpm -C frontend test
pnpm -C frontend e2e
```

After changing API contracts: build the API (rewrites `frontend/openapi/domolov.json`) then `pnpm -C frontend gen:api`. CI fails on drift in either file.

---

## Deploy

**Docker Compose** is the default. Three images: `domolov-api` (slim, no browser), `domolov-worker` (full Chromium), `domolov-web` (nginx + SPA). One worker. Do not scale it.

**Kubernetes** is a Kustomize base in [`deploy/k8s/`](deploy/k8s/): api (2) + worker (1) + web (2), Ingress that sends `/api` and `/health` to the API. Bring your own PostgreSQL. Guide: [Kubernetes](docs/guides/kubernetes.md).

**Reverse proxy.** Terminate TLS in front of port 8080. The SPA already proxies `/api`. SSE at `/api/scans/events` needs buffering off and a long read timeout. Guide: [Reverse proxy](docs/guides/reverse-proxy.md).

**Upgrading** from the old single Blazor image: [Upgrading](docs/guides/upgrading.md).

Images: `ghcr.io/mevljas/domolov-api`, `…-worker`, `…-web`. Keep the three tags in step — the api image owns migrations.

---

## API

Interactive docs (signed in, outside Development): [http://localhost:8080/api/docs](http://localhost:8080/api/docs).

```bash
curl -c jar -H 'X-Requested-With: domolov' -H 'Content-Type: application/json' \
  -d '{"password":"your-password"}' http://localhost:8080/api/session

curl -b jar 'http://localhost:8080/api/homes?sort=priceDrop'
```

Cookie session (`HttpOnly`, `SameSite=Strict`). Every unsafe method needs `X-Requested-With: domolov`. Errors are RFC 9457. Watches use weak ETags. Scans return `202` + `Location`. Live updates: `GET /api/scans/events`.

Details: [docs/api.md](docs/api.md).

---

## Tests

| Suite | How | Notes |
|---|---|---|
| Domain / Application / scanning | `dotnet test tests/Domolov.UnitTests` | HTML fixtures, no live site |
| PostgreSQL pipeline | `dotnet test tests/Domolov.IntegrationTests` | Testcontainers; Docker required |
| HTTP API | `dotnet test tests/Domolov.ApiTests` | Auth, CSRF, ETag, SSE, OpenAPI |
| Vue units | `pnpm -C frontend test` | Vitest + Vue Test Utils + MSW |
| Vue E2E | `pnpm -C frontend e2e` | Playwright, API mocked |
| Compose smoke | `pnpm -C frontend e2e:smoke` | Real stack, fixture provider |

We do not scrape live nepremicnine.net in tests.

---

## Architecture

```text
browser ──► domolov-web (nginx + Vue SPA)
                 │  /api  /health
                 ▼
            domolov-api  ◄──── PostgreSQL LISTEN/NOTIFY ────►  domolov-worker
                 │                                              │
                 └──────────── same schema, same binary ────────┘
                              (DOMOLOV_ROLE=api|worker|all)
```

| Piece | Where |
|---|---|
| Domain (no EF, no ASP.NET) | `src/Domolov.Domain` |
| Use-case handlers | `src/Domolov.Application` |
| EF, Playwright, notifiers, workers | `src/Domolov.Infrastructure` |
| Minimal APIs, Scalar, CLI | `src/Domolov.Api` |
| Vue 3 SPA | `frontend/` |

Decisions: [ADR 0004](docs/adr/0004-vue-spa-same-origin-proxy.md) (SPA), [0005](docs/adr/0005-api-worker-split-listen-notify.md) (api / worker), [0006](docs/adr/0006-new-headless-chromium-and-human-pacing.md) (browser), [0007](docs/adr/0007-home-aggregate-for-reposts.md) (Homes). Agent rules: [AGENTS.md](AGENTS.md).

---

## License

MIT — see [LICENSE](./LICENSE).
