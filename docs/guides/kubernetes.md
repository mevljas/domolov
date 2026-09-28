# Kubernetes

The manifests under [`deploy/k8s/`](../../deploy/k8s/) are a **Kustomize base**: three Deployments, two Services, one Ingress, a ConfigMap, a browser-profile PVC, and a Secret *example* that is not applied.

PostgreSQL is not included. Use a managed instance or something like CloudNativePG, then point `ConnectionStrings__Default` at it.

## Topology

| Deployment | Image | Replicas | Role |
|---|---|---|---|
| `domolov-api` | `ghcr.io/mevljas/domolov-api` | 2 | HTTP API. An init container runs `migrate` first. |
| `domolov-worker` | `ghcr.io/mevljas/domolov-worker` | 1 | Scheduler, Chromium, cleanup. `Recreate` (the profile PVC is RWO). |
| `domolov-web` | `ghcr.io/mevljas/domolov-web` | 2 | Vue SPA on nginx. |

The Ingress sends `/api` and `/health` to the api Service and everything else to web. Data Protection keys live in the database, so there is no key volume.

**Do not scale the worker past 1.** Scans and cron are not coordinated across pods.

## Deploy

1. Namespace + Secret. Credentials stay out of the directory on purpose, so `kubectl apply -k` cannot overwrite or commit them.

   ```bash
   kubectl create namespace domolov
   kubectl -n domolov create secret generic domolov-secrets \
     --from-literal=ConnectionStrings__Default='Host=…;Port=5432;Database=domolov;Username=domolov;Password=…' \
     --from-literal=DOMOLOV_ADMIN_PASSWORD_HASH="$(docker run --rm ghcr.io/mevljas/domolov-api hash-password 'your-password')"
   ```

   Add Telegram / SMTP / VAPID with more `--from-literal` flags, or copy [`secret.example.yaml`](../../deploy/k8s/secret.example.yaml) *outside the repo*, fill it in, and `kubectl apply -f` it. Pods stay in `CreateContainerConfigError` until the Secret exists.

2. Hostname. Replace `domolov.example.com` in [`ingress.yaml`](../../deploy/k8s/ingress.yaml) (both `tls.hosts` and `rules.host`) and `DOMOLOV_PUBLIC_URL` in [`configmap.yaml`](../../deploy/k8s/configmap.yaml). TLS uses the `domolov-tls` Secret; with cert-manager, uncomment the issuer annotation. Change `ingressClassName` if your controller is not `nginx`.

3. Apply:

   ```bash
   kubectl apply -k deploy/k8s
   ```

Preview with `kubectl kustomize deploy/k8s`.

## Overlay instead of editing the base

```yaml
# my-overlay/kustomization.yaml
resources:
  - github.com/mevljas/domolov/deploy/k8s?ref=v1.2.3
images:
  - name: ghcr.io/mevljas/domolov-api
    newTag: "1.2.3"
  - name: ghcr.io/mevljas/domolov-worker
    newTag: "1.2.3"
  - name: ghcr.io/mevljas/domolov-web
    newTag: "1.2.3"
patches:
  - target: { kind: Ingress, name: domolov }
    patch: |
      - op: replace
        path: /spec/rules/0/host
        value: domolov.mydomain.com
      - op: replace
        path: /spec/tls/0/hosts/0
        value: domolov.mydomain.com
```

Keep the three image tags in step: the `migrate` init container (api image) owns the schema for all of them.

## SSE and ingress-nginx

The Ingress annotations disable proxy buffering and set a one-hour timeout, which `/api/scans/events` needs. Other controllers need the equivalent — see [reverse-proxy.md](reverse-proxy.md).

## Day two

- Settings live in the ConfigMap. The full variable list is [`.env.example`](../../.env.example).
- The worker's browser profile is a PVC (`pvc-browser-profile.yaml`). Deleting it resets the fingerprint and Cloudflare cookies.
- A headed worker is a Compose-only trick (`docker-compose.dev.yml`). The cluster image is headless.
- Health: `/health/live`, `/health/ready` on the api Service; `/nginx-health` on web.
