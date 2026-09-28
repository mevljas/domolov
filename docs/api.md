# REST API

Everything the web app does goes through a documented REST API under `/api`. The OpenAPI 3.1 document lives at `/api/openapi/v1.json` and an interactive reference (Scalar) at `/api/docs`. Outside `ASPNETCORE_ENVIRONMENT=Development` both require a signed-in session.

## Conventions

- **JSON** is camelCase; enums are camelCase strings (`"priceAsc"`, `"viewingScheduled"`); query-string enums accept the same spelling.
- **Errors** are [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem details (`application/problem+json`) with a `traceId`. Validation failures return `400` with an `errors` object keyed by field.
- **Methods**: `GET` reads, `POST` creates or triggers, `PATCH` updates a subset of fields (omitted fields stay unchanged), `PUT` sets a sub-resource (bookmark, dismissal, seen), `DELETE` removes.
- **Asynchronous work** returns `202 Accepted` with a `Location` header, e.g. `POST /api/watches/{id}/scans` → `Location: /api/scans/{scanRunId}`.
- **Paging** uses `page` and `pageSize` (max 100) and returns `{ items, total, page, pageSize }`.
- **Optimistic concurrency**: Watches and notification routes return a weak `ETag`. Send it back as `If-Match` on `PATCH`/`DELETE`; a stale tag returns `412 Precondition Failed`. `If-Match` is optional.

## Authentication

```bash
# Sign in (sets the domolov_session cookie)
curl -c jar -H 'X-Requested-With: domolov' -H 'Content-Type: application/json' \
  -d '{"password":"your-password"}' http://localhost:8080/api/session

# Use the session
curl -b jar http://localhost:8080/api/homes?sort=priceDrop
```

- The session cookie is `HttpOnly` and `SameSite=Strict`, and each session can be listed and revoked (`GET /api/sessions`, `DELETE /api/sessions/{id}`, `DELETE /api/sessions` to sign out everywhere).
- Every `POST`, `PUT`, `PATCH` and `DELETE` must send `X-Requested-With: domolov`. Browsers cannot add custom headers cross-site without a CORS preflight, which Domolov never allows, so this blocks cross-site request forgery.
- Sign-in is rate-limited to 5 attempts per minute per client IP (`429` with `Retry-After`).
- Unauthenticated API calls get `401`, never a redirect.

## Resources

| Area | Endpoints |
|---|---|
| Session | `GET/POST/DELETE /api/session`, `GET/DELETE /api/sessions`, `DELETE /api/sessions/{id}` |
| Watches | `GET/POST /api/watches`, `GET/PATCH/DELETE /api/watches/{id}`, `POST /api/watches/{id}/scans`, `POST /api/search-url-checks` |
| Notification routes | `GET/POST /api/watches/{id}/notification-routes`, `PATCH/DELETE …/{routeId}`, `POST …/{routeId}/test-deliveries` |
| Homes | `GET /api/homes`, `GET /api/homes/{id}`, `PUT/DELETE /api/homes/{id}/bookmark`, `PUT/DELETE /api/homes/{id}/dismissal`, `PUT /api/homes/{id}/seen`, `POST /api/homes/seen`, `POST /api/homes/{id}/listings`, `DELETE /api/homes/{id}/listings/{listingId}` |
| Bookmarks | `GET /api/bookmarks` |
| Matches | `GET /api/home-matches`, `PATCH /api/home-matches/{id}` |
| Listings | `GET /api/listings/{id}`, `DELETE /api/listings` |
| Scans | `GET /api/scans`, `GET /api/scans/{id}`, `GET /api/scans/{id}/artifacts`, `GET /api/scans/{id}/artifacts/{artifactId}`, `GET /api/scans/events` |
| System | `GET /api/dashboard`, `GET /api/settings`, `GET /api/storage`, `POST /api/storage/cleanups`, `POST /api/push-subscriptions`, `DELETE /api/push-subscriptions/{id}` |
| Health | `GET /health/live`, `GET /health/ready` (public) |

## Live updates (Server-Sent Events)

`GET /api/scans/events` streams:

- `ready` once, when the stream opens;
- `scanRun` with a full ScanRun object whenever a ScanRun is queued, starts, finishes, fails or is interrupted;
- `ping` every 25 seconds to keep proxies from closing the connection.

The web app falls back to polling `GET /api/scans?active=true` if the stream drops.

## Regenerating the client types

The API build writes `frontend/openapi/domolov.json`. Then:

```bash
pnpm -C frontend gen:api
```

CI fails if either file is out of date.
