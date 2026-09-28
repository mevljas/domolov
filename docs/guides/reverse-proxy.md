# Reverse proxy and TLS

Compose publishes the **web** container on port **8080**. That container is nginx: it serves the Vue SPA and reverse-proxies `/api` and `/health` to the api service. You normally terminate TLS in front of 8080 and leave the containers on HTTP inside the network.

Kubernetes does the split at the Ingress: `/api` and `/health` go to the api Service, everything else to web. See [kubernetes.md](kubernetes.md).

## What the proxy must do

1. **TLS** for any host that is not localhost.
2. **Forward the original scheme and host** so cookies and redirects stay correct. The web nginx already honours `X-Forwarded-Proto` / `X-Forwarded-Host`.
3. **Do not buffer** `/api/scans/events`. That is a Server-Sent Events stream (`text/event-stream`) that pings every 25 s and can sit open for an hour. Buffering turns it into a stuck GET.
4. **Long read timeout** on that path (an hour is what the Compose / ingress-nginx annotations use).
5. **WebSocket-style HTTP/1.1** to the upstream (no `Connection: close`). The web nginx already does this for `/api`.

Set `DOMOLOV_PUBLIC_URL` to the public origin (`https://domolov.example.com`, no trailing slash) so notification links point at you, not at `localhost`.

## Caddy

```caddyfile
domolov.example.com {
    reverse_proxy localhost:8080
}
```

Caddy does not buffer by default. Flushing SSE through it just works. Point DNS at the box and let Caddy fetch the certificate.

## nginx (in front of Compose)

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    ""      close;
}

server {
    listen 443 ssl http2;
    server_name domolov.example.com;

    # ssl_certificate / ssl_certificate_key … (or include a Let's Encrypt snippet)

    location /api/scans/events {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Connection "";
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-For $proxy_addr;
        proxy_buffering    off;
        proxy_cache        off;
        proxy_read_timeout 3600s;
    }

    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-For $proxy_addr;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection $connection_upgrade;
    }
}
```

## Traefik

Label the web service (or a Traefik file provider) with a router on `Host(\`domolov.example.com\`)`. Disable buffering on the SSE path:

```yaml
http:
  middlewares:
    sse:
      buffering:
        maxResponseBodyBytes: 0
  routers:
    domolov:
      rule: Host(`domolov.example.com`)
      service: domolov
      tls: { certResolver: le }
    domolov-sse:
      rule: Host(`domolov.example.com`) && PathPrefix(`/api/scans/events`)
      service: domolov
      middlewares: [sse]
      tls: { certResolver: le }
```

## Security notes

- The operator password is **not** a substitute for TLS and a closed admin port.
- Cookies are `HttpOnly` + `SameSite=Strict`. They will not be sent cross-site; do not put the UI on a different origin than `/api` unless you are ready to redesign auth.
- The web image already sends a conservative CSP and the usual security headers. Do not strip them at the outer proxy.
- Health checks that must not require a cookie: `/health/live`, `/health/ready` (api) and `/nginx-health` (web).
