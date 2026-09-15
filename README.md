# Domolov

**Domolov** (*dom* = home + *lov* = hunt) is a self-hosted service that watches real-estate listing search URLs, stores results and price history, and notifies you of new finds and price changes.

Primary site adapter: [nepremicnine.net](https://www.nepremicnine.net/). Providers are pluggable so more sites can be added later.

## Features

- Paste a full search URL as a **Watch** (filters stay in the URL)
- Per-Watch cron schedule + **Run now**
- Concurrent scans (configurable)
- Persistent PostgreSQL storage with price history graphs
- Bookmarks
- Notifications: Discord webhooks, Telegram, email (SMTP), browser Web Push
- Per-route triggers: new listing, price down/up, any price change
- First successful scan is a silent **baseline** (no notification flood)
- Slovenian + English UI (all strings localized)
- Docker Compose + Kubernetes manifests
- OpenTelemetry (OTLP) when configured
- Structured Serilog logging + ScanRun history in the UI

## Quick start (Docker Compose)

```bash
cp .env.example .env
# set DOMOLOV_ADMIN_PASSWORD and other secrets
docker compose up -d
```

Open http://localhost:8080 and sign in with the admin password.

## Local development

Requires .NET 10 SDK (system-wide or project-local via `./install-dotnet.ps1` / `./install-dotnet.sh`).

```bash
dotnet restore
dotnet run --project src/Domolov
```

Set a Postgres connection string (or use Compose for DB only):

```bash
docker compose up -d postgres
```

Example env:

```bash
ConnectionStrings__Default=Host=localhost;Port=5432;Database=domolov;Username=domolov;Password=domolov
DOMOLOV_ADMIN_PASSWORD=changeme
DOMOLOV_TIMEZONE=Europe/Ljubljana
DOMOLOV_MAX_CONCURRENT_SCANS=2
DOMOLOV_BROWSER_HEADLESS=true
DOMOLOV_LOG_LEVEL=Information
```

## Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `DOMOLOV_ADMIN_PASSWORD` | Cookie-auth password for the operator | required |
| `ConnectionStrings__Default` | PostgreSQL connection string | required |
| `DOMOLOV_TIMEZONE` | IANA timezone for cron | `Europe/Ljubljana` |
| `DOMOLOV_MAX_CONCURRENT_SCANS` | Parallel Playwright scans | `2` |
| `DOMOLOV_BROWSER_HEADLESS` | Headless Chromium (local debug) | `false` in prod images |
| `DOMOLOV_BROWSER_USER_DATA_DIR` | Persistent browser profile path | `/data/browser-profile` |

After container recreate, Domolov clears stale Chromium `Singleton*` locks in that directory automatically. If scans still fail with “profile appears to be in use”, remove the Compose/K8s browser-profile volume and start again.
| `DOMOLOV_DATA_PROTECTION_KEYS_DIR` | Directory for ASP.NET Data Protection keys (must survive restarts or login cookies stop working) | `/data/data-protection-keys` |
| `DOMOLOV_LOG_LEVEL` | Serilog minimum level | `Information` |
| `DOMOLOV_ROLE` | `all`, `web`, or `worker` | `all` |
| `DOMOLOV_TELEGRAM_BOT_TOKEN` | Telegram bot token | optional |
| `DOMOLOV_SMTP_HOST` / `PORT` / `USER` / `PASSWORD` / `FROM` | SMTP | optional |
| `DOMOLOV_VAPID_PUBLIC_KEY` / `PRIVATE_KEY` / `SUBJECT` | Web Push | optional |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OpenTelemetry OTLP endpoint | optional |

Discord webhook URLs are configured per Watch notification route in the UI (not via env). Step-by-step: [docs/discord-notifications.md](docs/discord-notifications.md).

Put Domolov behind a reverse proxy with TLS. The admin password is a convenience boundary, not a substitute for network security.

Compose and the Kubernetes manifest mount a volume at `DOMOLOV_DATA_PROTECTION_KEYS_DIR` so cookie auth tickets remain valid across container restarts and redeploys.

## Watches

1. Build a search on nepremicnine.net (SL or EN URL — they differ).
2. Copy the results URL into Domolov.
3. Set the schedule (hourly / every 6 or 12 hours / days + time; stored as cron; times use `DOMOLOV_TIMEZONE`) and notification routes.
4. First scan seeds listings silently; later scans notify per your triggers.

## Notifications

Server-wide credentials come from env. Each Watch has **NotificationRoutes** choosing channel, destination, and triggers. For Discord webhooks, see [docs/discord-notifications.md](docs/discord-notifications.md).

## Kubernetes

See [`deploy/k8s/`](deploy/k8s/) for Deployment, Service, PVC, ConfigMap, Secret, and Ingress examples. Image: `ghcr.io/mevljas/domolov`.

## Development tooling

```bash
dotnet tool restore
dotnet csharpier check .
dotnet build
dotnet test --collect:"XPlat Code Coverage"
```

## License

MIT — see [LICENSE](./LICENSE).
