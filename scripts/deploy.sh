#!/usr/bin/env bash
# Despliegue completo de AdivinaQue en Azure Container Apps: preflight, build+publish de
# la imagen a ghcr.io, validate + what-if obligatorio con confirmación explícita, create,
# smoke test real (HTTP + WebSocket), y rollback automático a la revisión anterior si el
# smoke test falla. Idempotente: correrlo de nuevo sobre el mismo resource group
# actualiza en vez de duplicar.
#
# Uso:
#   ./deploy.sh --subscription <id-o-nombre> [opciones]
#
# Opciones:
#   --subscription <id|nombre>   Obligatorio. Nunca se usa la suscripción default.
#   --resource-group <nombre>    Default: rg-adivinaquien-prod
#   --location <región>          Default: brazilsouth
#   --project-name <nombre>      Default: adivinaquien
#   --environment <entorno>      Default: prod
#   --image-tag <tag>            Default: SHA corto de HEAD
#   --skip-build                 No buildear/pushear la imagen; usa --image-tag existente en ghcr.io
#   --yes                        No pedir confirmación interactiva tras el what-if

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

SUBSCRIPTION=""
RESOURCE_GROUP="rg-adivinaquien-prod"
LOCATION="brazilsouth"
PROJECT_NAME="adivinaquien"
ENVIRONMENT="prod"
IMAGE_TAG=""
SKIP_BUILD=0
ASSUME_YES=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --subscription) SUBSCRIPTION="$2"; shift 2 ;;
    --resource-group) RESOURCE_GROUP="$2"; shift 2 ;;
    --location) LOCATION="$2"; shift 2 ;;
    --project-name) PROJECT_NAME="$2"; shift 2 ;;
    --environment) ENVIRONMENT="$2"; shift 2 ;;
    --image-tag) IMAGE_TAG="$2"; shift 2 ;;
    --skip-build) SKIP_BUILD=1; shift ;;
    --yes) ASSUME_YES=1; shift ;;
    *) echo "Argumento desconocido: $1" >&2; exit 2 ;;
  esac
done

[[ -n "$SUBSCRIPTION" ]] || { echo "Uso: deploy.sh --subscription <id-o-nombre> [opciones]" >&2; exit 2; }

log() { echo "[deploy] $*"; }
fail() {
  echo "[deploy] ERROR: $*" >&2
  exit 1
}

cd "$REPO_ROOT"

log "=== 1/8 — Preflight ==="
"$SCRIPT_DIR/preflight.sh" --subscription "$SUBSCRIPTION" --location "$LOCATION"

az account set --subscription "$SUBSCRIPTION" || fail "No se pudo cambiar a la suscripción '$SUBSCRIPTION'."

log "=== 2/8 — Resource group '$RESOURCE_GROUP' ==="
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --tags "proyecto=$PROJECT_NAME" "entorno=$ENVIRONMENT" "gestionado-por=bicep" \
  --output none \
  || fail "No se pudo crear/actualizar el resource group '$RESOURCE_GROUP'."
log "   OK."

REMOTE_URL=$(git -C "$REPO_ROOT" remote get-url origin 2>/dev/null) \
  || fail "No hay remote 'origin' configurado — necesito owner/repo de GitHub para armar el nombre de la imagen en ghcr.io."
OWNER_REPO=$(echo "$REMOTE_URL" | sed -E 's#(git@|https://)github.com[:/]##; s#\.git$##' | tr '[:upper:]' '[:lower:]')
[[ -n "$IMAGE_TAG" ]] || IMAGE_TAG=$(git -C "$REPO_ROOT" rev-parse --short HEAD)
IMAGE="ghcr.io/${OWNER_REPO}:${IMAGE_TAG}"
log "   Imagen: $IMAGE"

if [[ "$SKIP_BUILD" == "0" ]]; then
  log "=== 3/8 — Build + push de la imagen ==="
  docker build -t "$IMAGE" -t "ghcr.io/${OWNER_REPO}:latest" "$REPO_ROOT" \
    || fail "docker build falló."
  docker push "$IMAGE" || fail "docker push de '$IMAGE' falló. ¿'docker login ghcr.io' está vigente?"
  docker push "ghcr.io/${OWNER_REPO}:latest" || fail "docker push de ':latest' falló."
  log "   OK."
else
  log "=== 3/8 — Build + push (omitido por --skip-build) ==="
fi

log "=== 4/8 — az bicep build (sintaxis) ==="
az bicep build --file "$REPO_ROOT/infra/main.bicep" --stdout >/dev/null \
  || fail "az bicep build encontró errores de sintaxis/esquema en main.bicep."
log "   OK."

PARAMS=(
  --parameters "$REPO_ROOT/infra/main.parameters.json"
  --parameters "containerImage=$IMAGE" "location=$LOCATION" "projectName=$PROJECT_NAME" "environment=$ENVIRONMENT"
)

log "=== 5/8 — az deployment group validate ==="
az deployment group validate \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$REPO_ROOT/infra/main.bicep" \
  "${PARAMS[@]}" \
  --output none \
  || fail "az deployment group validate falló — ver el error de arriba."
log "   OK."

log "=== 6/8 — az deployment group what-if ==="
az deployment group what-if \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$REPO_ROOT/infra/main.bicep" \
  "${PARAMS[@]}" \
  --result-format ResourceIdOnly

if [[ "$ASSUME_YES" == "0" ]]; then
  read -r -p "¿Aplicar estos cambios contra la suscripción real? Escribí 'SI' para continuar: " CONFIRM
  [[ "$CONFIRM" == "SI" ]] || fail "Cancelado por el usuario (no se escribió 'SI')."
fi

log "=== 7/8 — az deployment group create ==="
DEPLOYMENT_NAME="adivinaquien-deploy-$(date +%Y%m%d%H%M%S)"
OUTPUTS_JSON=$(az deployment group create \
  --name "$DEPLOYMENT_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$REPO_ROOT/infra/main.bicep" \
  "${PARAMS[@]}" \
  --query "properties.outputs" -o json) \
  || fail "az deployment group create falló — ver el error de arriba. La infraestructura previa (si existía) sigue intacta."

CONTAINER_APP_FQDN=$(echo "$OUTPUTS_JSON" | python3 -c "import json,sys; print(json.load(sys.stdin)['containerAppFqdn']['value'])" 2>/dev/null) \
  || fail "No se pudo leer 'containerAppFqdn' de los outputs del despliegue."
STORAGE_ACCOUNT_NAME=$(echo "$OUTPUTS_JSON" | python3 -c "import json,sys; print(json.load(sys.stdin)['storageAccountName']['value'])" 2>/dev/null || echo "?")
APP_URL="https://${CONTAINER_APP_FQDN}"
CONTAINER_APP_NAME="${PROJECT_NAME}-${ENVIRONMENT}-app"
log "   OK. URL: $APP_URL"

log "=== 8/8 — Smoke test (con reintentos por arranque en frío) ==="
SMOKE_OK=0
for attempt in 1 2 3 4 5 6; do
  if "$SCRIPT_DIR/smoke-test.sh" --url "$APP_URL"; then
    SMOKE_OK=1
    break
  fi
  log "   Intento $attempt falló, reintentando en 20s (arranque en frío puede tardar)..."
  sleep 20
done

if [[ "$SMOKE_OK" == "0" ]]; then
  log "!!! Smoke test falló tras varios intentos. Intentando rollback a la revisión anterior..."
  REVISIONS=$(az containerapp revision list \
    --name "$CONTAINER_APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query "sort_by([], &properties.createdTime)[].name" -o tsv 2>/dev/null || echo "")
  REVISION_COUNT=$(echo "$REVISIONS" | grep -c . || true)
  if [[ "$REVISION_COUNT" -lt 2 ]]; then
    fail "No hay una revisión anterior a la cual volver (este era el primer despliegue). Revisa los logs con 'az containerapp logs show --name $CONTAINER_APP_NAME --resource-group $RESOURCE_GROUP'."
  fi
  PREVIOUS_REVISION=$(echo "$REVISIONS" | tail -n 2 | head -n 1)
  az containerapp revision activate --revision "$PREVIOUS_REVISION" --resource-group "$RESOURCE_GROUP" --output none
  az containerapp ingress traffic set --name "$CONTAINER_APP_NAME" --resource-group "$RESOURCE_GROUP" \
    --revision-weight "${PREVIOUS_REVISION}=100" --output none
  fail "Rollback aplicado a la revisión '$PREVIOUS_REVISION'. El despliegue nuevo NO quedó activo — revisa la imagen '$IMAGE' antes de reintentar."
fi

log ""
log "=========================================="
log " Despliegue OK"
log " URL:               $APP_URL"
log " Resource group:     $RESOURCE_GROUP"
log " Storage account:     $STORAGE_ACCOUNT_NAME"
log " Imagen desplegada:  $IMAGE"
log "=========================================="
