# Domolov frontend

Vue 3 single-page app for Domolov (the self-hosted real-estate hunter). It talks to the .NET backend over
`/api` and is served as static files from `dist/`.

## Requirements

- Node 24 (`.nvmrc`)
- pnpm via Corepack: `corepack enable` (the version is pinned in `package.json#packageManager`)

## Scripts

| Script                         | What it does                                                                         |
| ------------------------------ | ------------------------------------------------------------------------------------ |
| `pnpm dev`                     | Vite dev server on :5173; proxies `/api` and `/health` to `http://localhost:5080`    |
| `pnpm build`                   | `vue-tsc -b && vite build` → `dist/` (includes `sw.js` and `manifest.webmanifest`)   |
| `pnpm preview`                 | Serve `dist/` on :4173                                                               |
| `pnpm lint` / `lint:fix`       | ESLint (flat config: eslint-plugin-vue, typescript-eslint, eslint-config-prettier)   |
| `pnpm format` / `format:check` | Prettier (with the Tailwind class sorter)                                            |
| `pnpm typecheck`               | `vue-tsc -b` across app, service worker, node and test configs                       |
| `pnpm test`                    | Vitest (jsdom, msw)                                                                  |
| `pnpm test:coverage`           | Vitest with v8 coverage; 80% line threshold on `src/shared/lib` and `**/composables` |
| `pnpm e2e`                     | Playwright against `vite preview`, API mocked with `page.route` (desktop + Pixel 7)  |
| `pnpm gen:api`                 | `openapi/domolov.json` → `src/api/schema.d.ts` (openapi-typescript)                  |
| `pnpm gen:pwa-assets`          | Regenerate PWA icons in `public/` from `public/favicon.svg`                          |

First Playwright run: `pnpm exec playwright install chromium`.

Point the dev proxy elsewhere with `DOMOLOV_API_ORIGIN=http://host:port pnpm dev`.

## Structure

```text
src/
  app/          bootstrap: main.ts, App.vue, router (auth guard), query client, i18n, PWA registration
  api/          openapi-fetch client (401 → /login), ProblemDetails helpers, SSE wrapper, schema.d.ts
  features/     one folder per area (auth, dashboard, watches, homes, matches, bookmarks, scans, settings)
  shared/
    components/ app shell and reusable pieces; ui/ holds shadcn-vue (Reka UI) primitives
    composables/ useTheme, useLocale, useViewTransition, useOnline, useToast, useAnnouncer, …
    lib/        pure helpers: cron (port of WatchCronSchedule), format, redirect, locale, cn
  stores/       Pinia — client preferences only (theme, locale)
  i18n/locales/ sl.json (default) and en.json
  styles/       main.css — Tailwind v4 @theme design tokens (light + dark)
  sw.ts         service worker (precache, SPA fallback, Web Push)
tests/e2e/      Playwright specs
```

## Conventions

- `<script setup lang="ts">`, blocks ordered script → template → style; typed `defineProps`/`defineEmits`.
- Route views stay thin; logic lives in `use*` composables. Server state goes through vue-query, never Pinia.
- Every user-visible string goes through vue-i18n. Keys are camelCase and grouped by area (`nav.*`,
  `watches.*`, `common.*`). Slovenian is the default; write it as natural Slovenian, gender-neutral where possible.
- Use the domain language from [`CONTEXT.md`](../CONTEXT.md) (Watch, ScanRun, Listing, Home, Bookmark, …).
- Colours come from the tokens in `styles/main.css` (`bg-surface`, `text-muted-foreground`, `text-terracotta-foreground`, …);
  use `numeric` for prices so digits align. Text-safe token variants meet WCAG AA.
- Motion: CSS transitions / `<Transition>` first, View Transitions for routes, `motion-v` only for small springs.
  Everything must respect `prefers-reduced-motion`.
- Add shadcn-vue components with `pnpm dlx shadcn-vue@latest add <name>` (configured by `components.json`).
- Tests never hit a real backend: use msw in Vitest and `page.route` in Playwright.
