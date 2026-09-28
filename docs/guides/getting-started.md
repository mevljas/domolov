# Getting started

This walks you from a cloned repo to a Watch that has completed its silent baseline and is waiting for the next scan.

## 1. Prerequisites

- Docker with Compose v2
- A password you will actually remember (or a password manager)
- A nepremicnine.net search you care about, **or** the smoke overlay if you only want to click around

## 2. Configure

```bash
cp .env.example .env
```

Set at least:

```dotenv
DOMOLOV_ADMIN_PASSWORD=pick-something-long
POSTGRES_PASSWORD=also-not-the-default
```

Optional but useful once you expose the app:

```dotenv
DOMOLOV_PUBLIC_URL=https://domolov.example.com
DOMOLOV_TIMEZONE=Europe/Ljubljana
```

To store a hash instead of the plaintext password:

```bash
docker compose run --rm api hash-password 'pick-something-long'
```

Put the printed `pbkdf2-sha256$…` line in `DOMOLOV_ADMIN_PASSWORD_HASH`. The hash wins if both are set.

## 3. Start

```bash
docker compose up -d
```

Wait until `api`, `worker` and `web` are healthy (`docker compose ps`). Open [http://localhost:8080](http://localhost:8080) and sign in.

The first request after a fresh database runs migrations automatically via the `migrate` one-shot container.

## 4. Create a Watch

1. On [nepremicnine.net](https://www.nepremicnine.net/) (or the English site) build a search the way you would by hand: type, price, rooms, area.
2. Copy the **results** URL from the address bar. Slovenian and English URLs are different pages — paste the one you want; Domolov will not translate it.
3. In Domolov open **Watches** → **New Watch**.
4. Paste the URL. The wizard checks it (`POST /api/search-url-checks`) and suggests a name.
5. Pick a schedule. Times use `DOMOLOV_TIMEZONE`. A six-hourly Watch does not fire exactly on the hour — Domolov adds a stable jitter of up to 20 % of the interval.
6. Optionally add a notification route (you can do this later). The first scan will not send anything anyway.

Save. The worker queues a ScanRun immediately.

## 5. Wait for baseline

Open **Scans**. The first successful run is **baseline**: listings are stored, Homes are created, no notification is sent. That is on purpose — you do not want 80 Discord messages the night you install this.

Statuses you will see:

| Status | Meaning |
|---|---|
| queued | Waiting for a worker slot |
| running | Chromium is walking the results pages |
| baseline | First success for this Watch |
| succeeded | Later success |
| failed | Error or Cloudflare block (open the run for artifacts) |
| interrupted | Process died mid-scan; recovered so the Watch can run again |

A scan of a real search takes minutes, not seconds. The worker dwells 5–15 s per page and pauses `DOMOLOV_SCAN_COOLDOWN_MS` between runs. That is the point.

## 6. Look at Homes

**Homes** is the feed. Each card is a dwelling, not an ad. Badges you will meet:

- **Unseen** — you have not opened it (a later price drop makes it unseen again)
- **Off market** — every ad of this Home was missed twice
- **Reposted** — a new ad appeared after every earlier one was delisted
- **N ads** — more than one Listing is linked (agency duplicates, or a history of reposts)

Open a Home for the timeline, the price chart, and the bookmark panel.

## 7. Turn on notifications

Pick a channel and follow its guide:

- [Discord](discord.md) — incoming webhook, no bot
- [Telegram](telegram.md) — one bot token in env, chat id on the route
- [Email](email.md) — SMTP
- [Web Push](web-push.md) — VAPID keys + the browser toggle in Settings

Use **Send test** on the route before you trust a live scan. Then wait for the *next* ScanRun (or press **Run now**). Baseline never notifies.

## 8. Day-two habits

- **Pause** a Watch you are not actively hunting. Listings and routes stay; the scheduler skips it; Run now still works.
- **Dismiss** Homes you will never want, including future reposts. That is different from a Bookmark in stage Rejected (Rejected still notifies on price).
- **Matches** is the review queue for scores between “possible” and “auto-link”. Keyboard: **S** same Home, **D** different.
- **Settings → Storage** shows database size, the last cleanup, and a **Run cleanup now** button. Bookmarked Homes are kept.

## If something is wrong

| Symptom | Where to look |
|---|---|
| Cannot sign in | `DOMOLOV_ADMIN_PASSWORD` / `_HASH` on *both* api and worker; you did restart after editing `.env` |
| Watch stays queued | `docker compose logs worker`; headed mode in a prod image will refuse to start |
| Cloudflare block | Scans → the failed run → screenshot / HTML / headers. Then [docs/scraping.md](../scraping.md) |
| No notifications after baseline | Route enabled? Destination correct? Trigger includes what happened? |
| UI loads, API 502 | `DOMOLOV_API_UPSTREAM` (Compose default `http://api:8080`) |

When you are ready to put this on a hostname: [reverse proxy](reverse-proxy.md) or [Kubernetes](kubernetes.md).
