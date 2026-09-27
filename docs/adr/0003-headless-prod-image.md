# Production image ships only the headless Chromium shell

The published image (`Dockerfile`, `ghcr.io/mevljas/domolov`) installs Playwright's `chromium-headless-shell` only, with no full Chromium, Xvfb, or apt Node, and defaults to `DOMOLOV_BROWSER_HEADLESS=true`. It also installs an explicit apt list (no Xvfb, no CJK/X bitmap fonts) and drops Mesa, which the headless shell never loads. This takes the image from about 1.75 GB to about 0.8 GB. Headed Chromium under Xvfb lives in `Dockerfile.dev` (via `docker-compose.dev.yml`) for debugging; the prod image refuses to start headed rather than silently falling back.

We accept that headless may be fingerprinted more readily by Cloudflare. The switch is gated on a manual ScanRun against nepremicnine.net that succeeds on the headless image; a CloudflareBlock regression that the headed `Dockerfile.dev` image does not hit is the trigger to revisit.

## Considered options

- **Alpine**: rejected. Playwright's bundled Chromium is glibc-linked and `install-deps` is apt-only; the Alpine `chromium` package would drift from the Chromium version Playwright pins and is unsupported.
- **Chiseled / distroless**: rejected. No shell or package manager, so the ~100 shared libraries the headless shell needs would have to be hand-copied, which is fragile for about 80 MB of savings.
- **Split web/worker images**: deferred. Most deployments run `DOMOLOV_ROLE=all`, so one image is simpler.
