# Split api and worker roles, coordinated with PostgreSQL LISTEN/NOTIFY

## Status

Accepted.

## Context

Scanning needs Chromium (hundreds of MB, one browser profile, one replica), while the HTTP API is light and can scale. The old scheduler polled every 20 s inside the web process, so "Run now" could wait up to 20 s, and live progress was a 2 s DB poll per browser tab. A crash mid-scan left ScanRuns stuck in Running forever.

Alternatives considered:

- Single process with an in-memory queue: simplest, but ties the API to Chromium and cannot fan out events across replicas.
- A message broker (Redis, RabbitMQ): robust, but another service to run for a self-hosted, single-operator app.

## Decision

One backend host with `DOMOLOV_ROLE=api|worker|all`, built into two images from one Dockerfile: `domolov-api` (no browser) and `domolov-worker` (full Chromium). The database is the only shared infrastructure. PostgreSQL `NOTIFY` carries three signals: `domolov_scan_requested` (wakes the worker's queue immediately), `domolov_scan_run_changed` (the API turns it into Server-Sent Events for every open tab) and `domolov_cleanup_requested`. Each process keeps one `LISTEN` connection and republishes on an in-process bus. The worker also polls every 60 s as a safety net. A graceful stop sweeps every Running ScanRun to Interrupted after in-flight scans are cancelled. A kill that cannot run that sweep is recovered the same way at the next worker start. Queued runs are left queued. `all` runs both roles in one process for local development and small installs.

## Consequences

- Run now starts within milliseconds; SSE works with any number of API replicas.
- Migrations run as a separate `migrate` step (Compose one-shot service, Kubernetes init containers) because several processes start at once; EF Core's migration lock makes concurrent runs safe.
- Signals are fire-and-forget: a missed NOTIFY is covered by the safety poll (worker) or the SPA's polling fallback (UI).
