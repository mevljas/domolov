# Domolov

| Setting | Value |
|---------|-------|
| **Interactivity Mode** | Server |
| **Interactivity Scope** | Router (`Routes`) + MudBlazor shell |
| **Target framework** | net10.0 |
| **Database** | PostgreSQL (EF Core) |
| **Scraping** | Microsoft.Playwright (headed Chromium + Xvfb in Docker) |

## Domain language

Read [CONTEXT.md](./CONTEXT.md) before renaming entities or inventing synonyms.

## Rendering

The Blazor router (`Routes`) uses Interactive Server so MudBlazor layout providers work. Prefer keeping interactivity at the shell; avoid adding extra circuits without reason. Login stays a plain HTML form (SSR-friendly auth contracts).

## Architecture

- `Domolov.Domain` — entities, enums, pure domain services
- `Domolov.Application` — use cases, DTOs, abstractions
- `Domolov.Infrastructure` — EF, Playwright providers, notifiers
- `Domolov` — Blazor host, minimal APIs, hosted worker

Do not scrape live nepremicnine.net in tests. Use HTML fixtures and fake `IListingProvider` implementations.

## Configuration

All server configuration is env-driven (`DOMOLOV_*`, connection strings, OTEL_*). Prefer env over committed secrets.

## Formatting

C# must match [CSharpier](https://csharpier.com/) (tool in `.config/dotnet-tools.json`). Before committing:

```bash
dotnet tool restore
dotnet csharpier format .
dotnet csharpier check .
```

CI and `CSharpierFormatTests` both fail on format drift. Do not push unformatted C#.

## Don'ts

- Prefer Playwright DOM/network event waits for page readiness. Bounded pacing (short page jitter and inter-scan cooldown) is allowed only to reduce Cloudflare hit rate — not as a substitute for waiting on real page signals.
- Do not rewrite pasted Watch URLs for language; English and Slovenian are different URLs.
- Do not call Discord bot gateway; use incoming webhooks.
- Do not add CAPTCHA solvers or proxy rotation in v1.
- Do not skip CSharpier format/check before push.
