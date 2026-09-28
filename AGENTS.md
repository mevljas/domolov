# Domolov

| Setting | Value |
|---------|-------|
| **Frontend** | Vue 3 SPA in `frontend/` (Vite, TypeScript strict, Tailwind CSS v4, shadcn-vue, TanStack Vue Query, vue-i18n), pnpm via Corepack, Node 24 |
| **Backend** | ASP.NET Core minimal APIs, `net10.0`, one host with roles `DOMOLOV_ROLE=api\|worker\|all` |
| **Database** | PostgreSQL (EF Core); LISTEN/NOTIFY for cross-process signals |
| **Scraping** | Microsoft.Playwright, full Chromium in new headless mode (worker image); headed Chromium + Xvfb via `Dockerfile.dev` |
| **API contract** | OpenAPI 3.1 exported at build time to `frontend/openapi/domolov.json`; TS types generated with `pnpm -C frontend gen:api` |

## Domain language

Read [CONTEXT.md](./CONTEXT.md) before renaming entities or inventing synonyms. Key terms: Watch, Paused Watch, ScanRun, Complete ScanRun, Home, Listing, Primary Listing, Repost, Duplicate Listing, HomeMatch, Bookmark, Dismissed Home, Unseen Home, Delisted, Purge.

## Architecture

- `src/Domolov.Domain`: entities with behaviour, organised by feature (`Watches`, `Listings`, `Homes`, `Scans`, `Notifications`, `Providers`, `Auth`, `Push`, `Retention`). No EF or ASP.NET references. Entities take `now` as a parameter; ids are UUID v7 (`Ids.New()`).
- `src/Domolov.Application`: one handler class per use case (`*Handler`, auto-registered), grouped by feature; contracts (request/response records) live next to their handlers. Use `TimeProvider`, never `DateTimeOffset.UtcNow`.
- `src/Domolov.Infrastructure`: EF Core (`Persistence/`), Playwright and fixture providers (`Scanning/`), notifiers, Postgres signals, background workers, DI.
- `src/Domolov.Api`: thin `Program.cs`, endpoint groups in `Endpoints/`, auth, OpenAPI/Scalar, health. CLI commands: `migrate`, `hash-password <pw>`, `browser-check`.
- `frontend/`: route views are thin composition surfaces; logic lives in `use*` composables; `<script setup lang="ts">`, typed props/emits, one `h1` per view, semantic HTML.

Architecture decisions are in `docs/adr/` (0004 SPA, 0005 api/worker split, 0006 browser realism, 0007 Homes).

## API rules

- REST under `/api`, camelCase JSON, camelCase string enums, RFC 9457 problem details, `PATCH` for partial updates, `202 + Location` for async work, weak ETags with `If-Match`.
- Unsafe methods require `X-Requested-With: domolov`. Unauthenticated calls return 401 (never redirects).
- After changing contracts, build the API (regenerates `frontend/openapi/domolov.json`) and run `pnpm -C frontend gen:api`. CI fails on drift in either file.

## Tests

- Do not scrape live nepremicnine.net in tests. Use HTML fixtures, `FixtureListingProvider` (`DOMOLOV_FAKE_PROVIDER=true`) and fake `IListingProvider`/`INotifier` implementations.
- Database tests use Testcontainers PostgreSQL (Docker required), not the EF in-memory provider.
- Frontend: Vitest + Vue Test Utils + MSW for units/components, Playwright with mocked API for E2E, and a Compose smoke suite against the fixture provider.

## Configuration

All server configuration is env-driven (`DOMOLOV_*`, `ConnectionStrings__Default`, `OTEL_*`); see `.env.example` and the README table. `DOMOLOV_RETENTION_*` and `DOMOLOV_MATCH_*` map to the `Domolov:Retention` and `Domolov:Match` sections. Prefer env over committed secrets.

## Formatting

C# must match [CSharpier](https://csharpier.com/) (tool in `.config/dotnet-tools.json`; `frontend/` is ignored). Frontend uses ESLint + Prettier.

```bash
dotnet tool restore
dotnet csharpier format .
dotnet csharpier check .
pnpm -C frontend lint
pnpm -C frontend format:check
```

CI and `CSharpierFormatTests` fail on C# format drift. Do not push unformatted code.

## Don'ts

- Wait on real Playwright signals (DOM/network, visible results) for readiness. Bounded human-like pacing (dwell 5–15 s, wheel scrolling, short pauses, cooldown jitter, schedule jitter) is allowed only after those waits, to reduce Cloudflare hit rate; never as a substitute for them.
- Do not rewrite pasted Watch URLs for language; English and Slovenian are different URLs.
- Do not call the Discord bot gateway; use incoming webhooks.
- Do not add CAPTCHA solvers, proxy rotation or stealth patch packs. `--disable-blink-features=AutomationControlled` on the Chromium launch is not one of those: it only clears the automation bit so the real browser matches a normal Chrome (ADR 0006).
- Do not reintroduce `navigator.webdriver` init-script overrides or hardcoded UA versions; the UA and client hints come from the real browser version.
- Do not skip CSharpier format/check before push.
