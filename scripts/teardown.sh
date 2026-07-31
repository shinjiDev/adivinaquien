#!/usr/bin/env bash
# Desmantela TODO lo desplegado por Fase 2 (Container App, Storage Account, Managed
# Identity, Container Apps Environment) y opcionalmente el budget — borra el resource
# group completo, que es la forma más simple de asegurarse de que no quede nada
# facturando. Pide confirmación explícita salvo --yes.
#
# Uso: ./teardown.sh --subscription <id-o-nombre> [--resource-group <nombre>] [--yes]

set -euo pipefail

SUBSCRIPTION=""
RESOURCE_GROUP="rg-adivinaquien-prod"
ASSUME_YES=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --subscription) SUBSCRIPTION="$2"; shift 2 ;;
    --resource-group) RESOURCE_GROUP="$2"; shift 2 ;;
    --yes) ASSUME_YES=1; shift ;;
    *) echo "Argumento desconocido: $1" >&2; exit 2 ;;
  esac
done

[[ -n "$SUBSCRIPTION" ]] || { echo "Uso: teardown.sh --subscription <id-o-nombre> [--resource-group <nombre>] [--yes]" >&2; exit 2; }

log() { echo "[teardown] $*"; }
fail() {
  echo "[teardown] ERROR: $*" >&2
  exit 1
}

az account set --subscription "$SUBSCRIPTION" || fail "No se pudo cambiar a la suscripción '$SUBSCRIPTION'."

if ! az group show --name "$RESOURCE_GROUP" >/dev/null 2>&1; then
  log "El resource group '$RESOURCE_GROUP' no existe — nada que borrar."
  exit 0
fi

log "Esto va a borrar TODO el resource group '$RESOURCE_GROUP': Container App, Storage"
log "Account (incluye las salas persistidas y las claves de Data Protection), Managed"
log "Identity, Container Apps Environment, y el budget/alertas de 1 USD."
az resource list --resource-group "$RESOURCE_GROUP" --query "[].{tipo:type, nombre:name}" -o table 2>/dev/null || true

if [[ "$ASSUME_YES" == "0" ]]; then
  read -r -p "Escribí el nombre del resource group ('$RESOURCE_GROUP') para confirmar el borrado: " CONFIRM
  [[ "$CONFIRM" == "$RESOURCE_GROUP" ]] || fail "Cancelado — el nombre escrito no coincide."
fi

log "Borrando '$RESOURCE_GROUP'..."
az group delete --name "$RESOURCE_GROUP" --yes --no-wait
log "Borrado en curso (no-wait) — 'az group show --name $RESOURCE_GROUP' devolverá error cuando termine."
log "Recordatorio: si publicaste una imagen en ghcr.io, este script no la borra — hacelo"
log "manualmente desde la página del paquete en GitHub si querés liberar ese espacio también."
