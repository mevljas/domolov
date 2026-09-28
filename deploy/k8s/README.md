# Kubernetes (Kustomize base)

Three Deployments in namespace `domolov`:

| Deployment | Image | Replicas | Notes |
|------------|-------|----------|-------|
| `domolov-api` | `ghcr.io/mevljas/domolov-api` | 2 | HTTP API. An init container runs `migrate` first. |
| `domolov-worker` | `ghcr.io/mevljas/domolov-worker` | 1 | Scheduler, Chromium scans, cleanup. `Recreate` strategy (RWO profile PVC). |
| `domolov-web` | `ghcr.io/mevljas/domolov-web` | 2 | Vue SPA on nginx. |

The Ingress sends `/api` and `/health` to the api Service and everything else to the web Service.

PostgreSQL is not included. Bring your own (managed, CloudNativePG, ...) and point `ConnectionStrings__Default` at it. Data Protection keys live in the database, so no key volume is needed.

## Deploy

1. Create the namespace and the Secret. Credentials are kept out of this directory on purpose, so `kubectl apply -k` never overwrites or commits them.

   ```bash
   kubectl create namespace domolov
   kubectl -n domolov create secret generic domolov-secrets \
     --from-literal=ConnectionStrings__Default='Host=...;Port=5432;Database=domolov;Username=domolov;Password=...' \
     --from-literal=DOMOLOV_ADMIN_PASSWORD_HASH="$(docker run --rm ghcr.io/mevljas/domolov-api hash-password 'your-password')"
   ```

   Add notification credentials with more `--from-literal` flags, or copy [`secret.example.yaml`](secret.example.yaml) outside the repo, fill it in and `kubectl apply -f` it. Pods stay in `CreateContainerConfigError` until the Secret exists.

2. Set your hostname: replace `domolov.example.com` in [`ingress.yaml`](ingress.yaml) (both `tls.hosts` and `rules.host`). TLS uses the `domolov-tls` Secret; with cert-manager, uncomment the issuer annotation. Change `ingressClassName` if your controller is not `nginx`.

3. Apply:

   ```bash
   kubectl apply -k deploy/k8s
   ```

Preview the rendered manifests with `kubectl kustomize deploy/k8s`.

## Customizing

Prefer an overlay over editing the base:

```yaml
# my-overlay/kustomization.yaml
resources:
  - ../deploy/k8s   # or a git URL with ?ref=v1.2.3
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

Settings live in [`configmap.yaml`](configmap.yaml); the full variable reference is the repo's `.env.example`.

## Notes

- Keep the api, worker and web image tags in step: the `migrate` init container (api image) owns the schema for all of them.
- The worker is a singleton. Do not scale it past 1; scans and cron scheduling are not coordinated across workers.
- The Ingress annotations are for ingress-nginx and disable proxy buffering with a one-hour timeout, which the Server-Sent Events stream at `/api/scans/events` needs. Other controllers need equivalent settings.
