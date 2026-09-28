# Production image ships only the headless Chromium shell

## Status

Superseded by [0005](0005-api-worker-split-listen-notify.md) (separate api / worker images) and [0006](0006-new-headless-chromium-and-human-pacing.md) (full Chromium in new headless mode).

## Context

The first published image (`Dockerfile`, `ghcr.io/mevljas/domolov`) installed Playwright's `chromium-headless-shell` only — no full Chromium, Xvfb, or apt Node — and defaulted to `DOMOLOV_BROWSER_HEADLESS=true`. That cut the image from about 1.75 GB to about 0.8 GB. Headed Chromium under Xvfb lived in `Dockerfile.dev`. The prod image refused to start headed rather than silently falling back.

We accepted that the shell is easier for Cloudflare to fingerprint. The gate to revisit was a CloudflareBlock on the headless image that the headed image did not hit.

## Decision (historical)

Ship one image with the headless shell. Keep a headed debug image. Do not use Alpine (Playwright's Chromium is glibc), chiseled/distroless (too many libraries to copy by hand), or split web/worker images yet (`DOMOLOV_ROLE=all` was the common case).

## Why it was superseded

The worker is now its own image (ADR 0005), so the API no longer pays for a browser. The shell reported `HeadlessChrome` in client hints while the UA string did not, which is a stronger bot signal than a slightly larger worker (ADR 0006). The production worker therefore installs full Chromium (`--with-deps --no-shell`) and runs new headless mode. Headed Chromium + Xvfb stays in `Dockerfile.dev`. Images today: `domolov-api` (no browser), `domolov-worker` (full Chromium), `domolov-web` (nginx + SPA).
