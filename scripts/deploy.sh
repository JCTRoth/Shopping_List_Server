#!/usr/bin/env bash
set -euo pipefail

# ── Configuration ─────────────────────────────────────────────────────────
PROJECT="shoppinglist-api"
SUBDOMAIN="api"
REGISTRY="${REGISTRY:-ghcr.io/danielroth1}"
IMAGE="${REGISTRY}/${PROJECT}"
TAG="${TAG:-latest}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# ── Load shared config ────────────────────────────────────────────────────
if [ -f "$REPO_ROOT/.env.deploy" ]; then
  source "$REPO_ROOT/.env.deploy"
elif [ -f "$REPO_ROOT/../.env.deploy" ]; then
  source "$REPO_ROOT/../.env.deploy"
else
  echo "WARNING: .env.deploy not found — set YOURUSER, GHCR_TOKEN, K3S_SERVER manually"
fi

KUBECONFIG="${KUBECONFIG:-$HOME/.kube/k3s-erp.yaml}"
export KUBECONFIG

echo "════════════════════════════════════════════════════════════"
echo " $PROJECT Deploy → k3s"
echo " Registry:   $REGISTRY"
echo " Tag:        $TAG"
echo " Namespace:  $PROJECT"
echo " Subdomain:  https://${SUBDOMAIN}.shopping-now.net"
echo "════════════════════════════════════════════════════════════"

# ── Verify cluster connectivity ───────────────────────────────────────────
echo ""
echo "▶ Verifying cluster connection..."
kubectl cluster-info

# ── Build & push ──────────────────────────────────────────────────────────
echo ""
echo "▶ Building & pushing ${IMAGE}:${TAG}..."
docker buildx build \
  --platform linux/amd64 \
  --tag "${IMAGE}:${TAG}" \
  --push \
  "$REPO_ROOT"
echo "  ✓ Pushed"

# ── Create namespace & pull secret ────────────────────────────────────────
echo ""
echo "▶ Ensuring namespace '$PROJECT'..."
kubectl create namespace "$PROJECT" --dry-run=client -o yaml | kubectl apply -f -

echo "▶ Creating/updating GHCR pull secret..."
kubectl create secret docker-registry ghcr-secret \
  --docker-server=ghcr.io \
  --docker-username="${YOURUSER}" \
  --docker-password="${GHCR_TOKEN}" \
  --namespace="$PROJECT" \
  --dry-run=client -o yaml | kubectl apply -f -
echo "  ✓ Pull secret ready"

# ── Application secrets ───────────────────────────────────────────────────
# Values are taken from .env.deploy (JWT_SECRET, DB_PASSWORD, MAIL_PASSWORD).
# Anything not provided is reused from the existing Secret in the cluster, and
# on the very first deploy the JWT signing key and DB password are generated.
read_secret() {
  kubectl get secret shoppinglist-api-secrets -n "$PROJECT" \
    -o go-template="{{ index .data \"$1\" }}" 2>/dev/null | openssl base64 -d -A 2>/dev/null || true
}

JWT_SECRET="${JWT_SECRET:-$(read_secret AppSettings__Secret)}"
if [ -z "$JWT_SECRET" ]; then
  JWT_SECRET="$(openssl rand -base64 48 | tr -d '\n')"
  echo "▶ Generated a new JWT signing key"
else
  echo "▶ Reusing existing JWT signing key"
fi

DB_PASSWORD="${DB_PASSWORD:-$(read_secret AppSettings__DbPassword)}"
if [ -z "$DB_PASSWORD" ]; then
  DB_PASSWORD="$(openssl rand -hex 24)"
  echo "▶ Generated a new database password"
fi

MAIL_PASSWORD="${MAIL_PASSWORD:-$(read_secret AppSettings__NoReplyEMailPassword)}"

# ── Object storage credentials (shared by MinIO and the API) ──────
MINIO_ACCESS_KEY="${MINIO_ACCESS_KEY:-$(read_secret AppSettings__ObjectStorage__AccessKey)}"
if [ -z "$MINIO_ACCESS_KEY" ]; then
  MINIO_ACCESS_KEY="shoppinglist"
fi

MINIO_SECRET_KEY="${MINIO_SECRET_KEY:-$(read_secret AppSettings__ObjectStorage__SecretKey)}"
if [ -z "$MINIO_SECRET_KEY" ]; then
  MINIO_SECRET_KEY="$(openssl rand -hex 24)"
  echo "▶ Generated new object-storage credentials"
else
  echo "▶ Reusing existing object-storage credentials"
fi

echo "▶ Creating/updating object storage secret..."
kubectl create secret generic shoppinglist-minio-secrets \
  --namespace="$PROJECT" \
  --from-literal=MINIO_ROOT_USER="$MINIO_ACCESS_KEY" \
  --from-literal=MINIO_ROOT_PASSWORD="$MINIO_SECRET_KEY" \
  --dry-run=client -o yaml | kubectl apply -f -
echo "  ✓ Object storage secret ready"

echo "▶ Creating/updating application secret..."
kubectl create secret generic shoppinglist-api-secrets \
  --namespace="$PROJECT" \
  --from-literal=AppSettings__Secret="$JWT_SECRET" \
  --from-literal=AppSettings__DbPassword="$DB_PASSWORD" \
  --from-literal=AppSettings__NoReplyEMailPassword="$MAIL_PASSWORD" \
  --from-literal=AppSettings__ObjectStorage__AccessKey="$MINIO_ACCESS_KEY" \
  --from-literal=AppSettings__ObjectStorage__SecretKey="$MINIO_SECRET_KEY" \
  --dry-run=client -o yaml | kubectl apply -f -
echo "  ✓ Application secret ready"

# ── Apply Kubernetes manifests ────────────────────────────────────────────
echo ""
echo "▶ Applying manifests..."
kubectl apply -f "$REPO_ROOT/k8s/"

# ── Wait for PostgreSQL, MinIO, then the API ───────────────────────
echo ""
echo "▶ Waiting for PostgreSQL..."
kubectl rollout status deployment/shoppinglist-api-postgres -n "$PROJECT" --timeout=300s

echo ""
echo "▶ Waiting for MinIO..."
kubectl rollout status deployment/shoppinglist-minio -n "$PROJECT" --timeout=300s

# "latest" with an unchanged manifest does not restart the pods, so force it
# to pick up the freshly pushed image.
echo ""
echo "▶ Restarting API to pull ${TAG}..."
kubectl rollout restart deployment/"$PROJECT" -n "$PROJECT"

echo "▶ Waiting for deployment..."
kubectl rollout status deployment/"$PROJECT" -n "$PROJECT" --timeout=300s

# ── Show status ───────────────────────────────────────────────────────────
echo ""
echo "▶ Pod status:"
kubectl get pods -n "$PROJECT" -o wide

echo ""
echo "▶ Ingress:"
kubectl get ingress -n "$PROJECT"

echo ""
echo "▶ Services:"
kubectl get services -n "$PROJECT"

echo ""
echo "▶ Certificate:"
kubectl get certificate -n "$PROJECT" || true

echo ""
echo "════════════════════════════════════════════════════════════"
echo " ✅  Deployment complete!"
echo ""
echo "  URL: https://${SUBDOMAIN}.shopping-now.net"
echo "  (TLS certificate may take 2-5 minutes on first deploy)"
echo ""
echo "  Check cert: KUBECONFIG=~/.kube/k3s-erp.yaml kubectl get certificate -n $PROJECT"
echo "  Logs:       KUBECONFIG=~/.kube/k3s-erp.yaml kubectl logs -n $PROJECT -f deploy/$PROJECT"
echo "════════════════════════════════════════════════════════════"
