#!/usr/bin/env bash
# Preflight completo de despliegue — extiende preflight-budget-check.sh (Fase 0) con las
# verificaciones que Fase 2+ necesita antes de tocar infraestructura real: providers de
# recursos registrados en la suscripción, y disponibilidad real de Container Apps en la
# región elegida (no se asume de memoria, se consulta contra la suscripción real).
#
# Uso: ./preflight.sh --subscription <id-o-nombre> [--location <región>] [--allow-spending-limit-off]
# Salida: 0 si todo OK para continuar (incluye el caso "aviso no bloqueante" de
# preflight-budget-check.sh, código 3, que acá se traduce a 0 con el aviso reimpreso).
# Cualquier otro caso aborta con código distinto de cero y un mensaje accionable.
#
# --allow-spending-limit-off: decisión explícita y ya tomada de continuar con el límite
# de gasto apagado (ver preflight-budget-check.sh --allow-off) — no es el default.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

SUBSCRIPTION=""
LOCATION="brazilsouth"
ALLOW_SPENDING_LIMIT_OFF=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --subscription)
      SUBSCRIPTION="$2"
      shift 2
      ;;
    --location)
      LOCATION="$2"
      shift 2
      ;;
    --allow-spending-limit-off)
      ALLOW_SPENDING_LIMIT_OFF=1
      shift
      ;;
    *)
      echo "Argumento desconocido: $1" >&2
      exit 2
      ;;
  esac
done

[[ -n "$SUBSCRIPTION" ]] || { echo "Uso: preflight.sh --subscription <id-o-nombre> [--location <región>]" >&2; exit 2; }

log() { echo "[preflight] $*"; }
fail() {
  echo "[preflight] ERROR: $*" >&2
  exit 1
}

log "Paso 1/3 — límite de gasto de la suscripción"
BUDGET_ARGS=(--subscription "$SUBSCRIPTION")
[[ "$ALLOW_SPENDING_LIMIT_OFF" == "1" ]] && BUDGET_ARGS+=(--allow-off)
set +e
"$SCRIPT_DIR/preflight-budget-check.sh" "${BUDGET_ARGS[@]}"
BUDGET_EXIT=$?
set -e
case "$BUDGET_EXIT" in
  0) log "Límite de gasto OK." ;;
  3) log "Aviso no bloqueante del chequeo de límite de gasto (ver arriba) — se continúa bajo tu criterio." ;;
  *) fail "preflight-budget-check.sh abortó (código $BUDGET_EXIT) — ver mensaje arriba." ;;
esac

az account set --subscription "$SUBSCRIPTION" || fail "No se pudo cambiar a la suscripción '$SUBSCRIPTION'."

log "Paso 2/3 — resource providers requeridos"
REQUIRED_PROVIDERS=(Microsoft.App Microsoft.ManagedIdentity Microsoft.Storage Microsoft.CostManagement Microsoft.Authorization)
for provider in "${REQUIRED_PROVIDERS[@]}"; do
  STATE=$(az provider show -n "$provider" --query registrationState -o tsv 2>/dev/null || echo "NotFound")
  if [[ "$STATE" == "Registered" ]]; then
    log "  $provider: Registered"
  else
    log "  $provider: $STATE — registrando (puede tardar un par de minutos)..."
    az provider register -n "$provider" --wait || fail "No se pudo registrar el provider $provider."
    log "  $provider: Registered"
  fi
done

log "Paso 3/3 — disponibilidad de Container Apps en '$LOCATION'"
# az -o tsv imprime un elemento por línea (no separados por tabs, al ser una sola
# columna) — el split debe ser por salto de línea, no por tab.
AVAILABLE_LOCATIONS=$(az provider show -n Microsoft.App \
  --query "resourceTypes[?resourceType=='managedEnvironments'].locations | [0]" -o tsv 2>&1) \
  || fail "No se pudo consultar las regiones disponibles para Microsoft.App/managedEnvironments."

# Comparación insensible a espacios/mayúsculas: az devuelve nombres como "Brazil South".
NORMALIZED_LOCATION=$(echo "$LOCATION" | tr -d ' ' | tr '[:upper:]' '[:lower:]')
FOUND=0
while IFS= read -r loc; do
  NORMALIZED_LOC=$(echo "$loc" | tr -d ' ' | tr '[:upper:]' '[:lower:]')
  if [[ "$NORMALIZED_LOC" == "$NORMALIZED_LOCATION" ]]; then
    FOUND=1
    break
  fi
done <<< "$AVAILABLE_LOCATIONS"

[[ "$FOUND" == "1" ]] || fail "Container Apps no está disponible en '$LOCATION' para esta suscripción. Regiones disponibles: $AVAILABLE_LOCATIONS"
log "Container Apps disponible en '$LOCATION'."

log "Preflight completo — listo para desplegar."
