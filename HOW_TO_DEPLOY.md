# Deploy the ShoppingListServer API

This project runs on the k3s cluster at `https://api.shopping-now.net`.

Everything lives in one Kubernetes namespace (`shoppinglist-api`):

| Resource | Name | Purpose |
|----------|------|---------|
| Deployment | `shoppinglist-api` | The ASP.NET Core 10 API (port 5678) |
| Service | `shoppinglist-api-service` | ClusterIP 80 → pod 5678 |
| Ingress | `shoppinglist-api-ingress` | Traefik + cert-manager TLS for `api.shopping-now.net` |
| Deployment | `shoppinglist-api-postgres` | PostgreSQL 16 (2 Gi volume) |
| Deployment | `shoppinglist-minio` | MinIO object storage for profile pictures (5 Gi volume) |
| Ingress | `shoppinglist-minio-ingress` | TLS for `media.shopping-now.net` (pre-signed URL host) |
| Secret | `shoppinglist-api-secrets` | JWT signing key, DB password, SMTP password, storage keys |
| Secret | `shoppinglist-minio-secrets` | MinIO root credentials (shared with the API) |

The API has **no PersistentVolumeClaim**: shopping lists are rows in PostgreSQL and profile pictures
are objects in MinIO, so the pod is stateless and updates roll.

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) with the buildx plugin
- [kubectl](https://kubernetes.io/docs/tasks/tools/) with the k3s kubeconfig at `~/.kube/k3s-erp.yaml`
- A DNS `A` record for `api.shopping-now.net` pointing at the k3s server
- Logged into `ghcr.io` — you need a GitHub PAT with `read:packages` and `write:packages`:

  ```bash
  echo "YOUR_GITHUB_TOKEN" | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin
  ```

## Setup

### 1. Configure environment variables

```bash
cp .env.example .env.deploy
```

Edit `.env.deploy`:

| Variable | Description |
|----------|-------------|
| `K3S_SERVER` | SSH target for the k3s server (e.g. `user@your-k3s-server-ip`) |
| `K3S_SSH_PORT` | SSH port (default: `7878`) |
| `YOURUSER` | Your GitHub username |
| `GHCR_TOKEN` | A GitHub PAT with `read:packages` + `write:packages` |
| `JWT_SECRET`, `DB_PASSWORD`, `MAIL_PASSWORD` | Optional. Leave empty on the first deploy — the script generates the JWT key and DB password, and reuses them from the cluster secret on later deploys |

> `.env.deploy` holds secrets — keep it out of git (it is already covered by `.gitignore` via the `.env*` pattern if present; otherwise add it).

### 2. Deploy

```bash
bash scripts/deploy.sh
```

The script builds the image for `linux/amd64`, pushes it to ghcr.io, creates the
namespace, the image-pull secret and the application secret, applies
`k8s/*.yaml`, waits for PostgreSQL, restarts the API pod so it pulls the new image,
and prints the status.

Or, in VS Code: **Terminal → Run Task… → k3s: deploy** (or `k3s: build-push-deploy`).

### Optional secrets for full functionality

Two things live in the app rather than the repo:

- **Apple Sign-In** — put the `.p8` key into the pod (e.g. as its own Secret
  mounted at `/app/auth/`) and set `AppSettings__AppleSignInKeyId`,
  `AppSettings__AppleTeamId` and `AppSettings__AppleSignInP8SecretPath` in
  `k8s/configmap.yaml`.
- **Push notifications (Firebase)** — set `UseFirebase: "true"` in
  `k8s/configmap.yaml` and mount the service-account JSON at the path in
  `GOOGLE_APPLICATION_CREDENTIALS` (`/app/auth/cert-files/...`).

Without them the API still starts and serves every non-Apple, non-push feature.

## Checking status

```bash
KUBECONFIG=~/.kube/k3s-erp.yaml kubectl get pods,ingress,svc,certificate -n shoppinglist-api -o wide
KUBECONFIG=~/.kube/k3s-erp.yaml kubectl logs -n shoppinglist-api -f deploy/shoppinglist-api
```

The API is reachable at `https://api.shopping-now.net`. TLS certificate
provisioning may take 2–5 minutes on the first deploy.

> The API applies EF Core migrations on startup. The pod waits for PostgreSQL and MinIO via init
> containers, and the first start can take a minute while migrations run. `scripts/deploy.sh`
> generates the MinIO credentials once and reuses them on later deploys.

## Updating

Run `bash scripts/deploy.sh` again — it builds a new image, pushes it, applies
the manifests and rolls the API pod. Shopping lists survive in PostgreSQL and profile pictures in
the MinIO volume.

## Troubleshooting

```bash
# Certificate not ready
kubectl describe certificaterequest -n shoppinglist-api
kubectl logs -n cert-manager deployment/cert-manager --tail=50

# Pod not starting / image pull issues
kubectl describe pod -n shoppinglist-api
kubectl get secret ghcr-secret shoppinglist-api-secrets -n shoppinglist-api

# API errors (DB connection, migrations)
kubectl logs -n shoppinglist-api deploy/shoppinglist-api
kubectl logs -n shoppinglist-api deploy/shoppinglist-api-postgres
```

Common causes:

- `ImagePullBackOff` → `GHCR_TOKEN` missing `read:packages`, or the image name
  is not lowercase (`ghcr.io/danielroth1/shoppinglist-api`).
- API crash-looping with a database error → check the `shoppinglist-api-secrets`
  secret and that `shoppinglist-api-postgres` is `1/1 Ready`.
- 404 from Traefik → service selector/targetPort mismatch (`app: shoppinglist-api`,
  `targetPort: 5678`).
