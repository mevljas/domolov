# Domolov

| Setting | Value |
|---------|-------|
| **Interactivity Mode** | Server |
| **Interactivity Scope** | Per-page |
| **Target framework** | net10.0 |
| **Database** | PostgreSQL (EF Core) |
| **Scraping** | Microsoft.Playwright (headed Chromium + Xvfb in Docker) |

## Domain language

Read [CONTEXT.md](./CONTEXT.md) before renaming entities or inventing synonyms.

## Rendering

Pages are static SSR by default. Add `@rendermode InteractiveServer` only where interactivity is required (forms, charts, live actions).

## Architecture

- `Domolov.Domain` — entities, enums, pure domain services
- `Domolov.Application` — use cases, DTOs, abstractions
- `Domolov.Infrastructure` — EF, Playwright providers, notifiers
- `Domolov` — Blazor host, minimal APIs, hosted worker

Do not scrape live nepremicnine.net in tests. Use HTML fixtures and fake `IListingProvider` implementations.

## Configuration

All server configuration is env-driven (`DOMOLOV_*`, connection strings, OTEL_*). Prefer env over committed secrets.

## Don'ts

- No hardcoded scrape delays — wait on Playwright DOM/network events.
- Do not rewrite pasted Watch URLs for language; English and Slovenian are different URLs.
- Do not call Discord bot gateway; use incoming webhooks.
- Do not add CAPTCHA solvers or proxy rotation in v1.
