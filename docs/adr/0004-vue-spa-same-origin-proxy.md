# Separate Vue SPA behind a same-origin proxy

## Status

Accepted. Supersedes [0002](0002-mudblazor-ui.md).

## Context

The Blazor Server UI called application services directly, held a SignalR circuit per tab, and made the UI hard to evolve independently of the backend (the `/api` surface existed but was incomplete). We wanted a richer, animated, installable UI with offline-friendly behaviour, a typed REST contract that other clients could use, and separate deploy units for UI and backend.

Alternatives considered:

- Keep Blazor Server and polish it: lowest churn, but still circuit-bound, no PWA, and the UI keeps bypassing the API.
- Blazor WebAssembly: stays in C#, but large payloads and a thinner ecosystem for the design and motion work we wanted.
- Vue 3 SPA with CORS on a separate origin: clean split, but cross-site cookies and preflights complicate cookie auth.

## Decision

Build the UI as a Vue 3 + TypeScript SPA in `frontend/` (Vite, Tailwind CSS v4, shadcn-vue, TanStack Vue Query, vue-i18n). Ship it as its own image (`domolov-web`): nginx serves the static build and reverse-proxies `/api` and `/health` to the API, so the browser sees one origin. Authentication stays a cookie session (HttpOnly, SameSite=Strict) plus a required `X-Requested-With: domolov` header on state-changing requests. The API contract is the OpenAPI document exported at build time into `frontend/openapi/domolov.json`; the SPA's TypeScript types are generated from it and CI fails on drift.

## Consequences

- No CORS configuration; cookies are first-party; the CSRF defence is the custom header plus SameSite=Strict.
- Every UI feature must go through the REST API, which keeps the API complete and documented (Scalar at `/api/docs`).
- Two toolchains (dotnet + pnpm) in development and CI.
- Kubernetes Ingress can route `/api` straight to the API service; Compose relies on the nginx proxy.
