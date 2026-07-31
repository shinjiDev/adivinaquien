#!/usr/bin/env bash
# Fase 0 — red de seguridad financiera. Verifica que el "límite de gasto" de la
# suscripción esté prendido ANTES de dejar correr cualquier despliegue. Este límite es
# lo único que corta servicios automáticamente si algo sale mal (a diferencia del budget
# de Bicep, que solo avisa por email); solo existe en suscripciones con crédito (Free
# Trial, Azure for Students, crédito de Visual Studio) y SOLO se puede prender/apagar
# desde el portal — no hay API de escritura para esto, por eso este script solo lee y
# aborta, nunca "arregla" el estado por su cuenta.
#
# Campo verificado contra la doc oficial de la REST API "Subscriptions - Get"
# (subscriptionPolicies.spendingLimit, enum On|Off|CurrentPeriodOff) — no existe
# subcomando dedicado de `az account`/`az billing` para esto, así que se consulta con
# `az rest` directo contra el Resource Manager.
#
# Uso: ./preflight-budget-check.sh [--subscription <id-o-nombre>] [--allow-off]
# Salida: 0 si el límite está On. Distinto de cero en cualquier otro caso (Off,
# CurrentPeriodOff, no aplica al tipo de suscripción, o no se pudo verificar) — el
# llamador (deploy.sh) debe tratar cualquier salida no-cero como "no continuar".
#
# --allow-off: baja Off/CurrentPeriodOff de abort duro a aviso (exit 0, con el mismo
# mensaje impreso). Es una decisión explícita del usuario, tomada una vez y pasada por
# flag — no un default, porque sin el límite de gasto la única red de seguridad real es
# la alerta de presupuesto de 1 USD por email (no corta nada automáticamente).

set -euo pipefail

SUBSCRIPTION=""
ALLOW_OFF=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --subscription)
      SUBSCRIPTION="$2"
      shift 2
      ;;
    --allow-off)
      ALLOW_OFF=1
      shift
      ;;
    *)
      echo "Argumento desconocido: $1" >&2
      exit 2
      ;;
  esac
done

log() { echo "[preflight] $*"; }
fail() {
  echo "[preflight] ERROR: $*" >&2
  exit 1
}

command -v az >/dev/null 2>&1 || fail "Azure CLI no está instalado. Instálalo antes de continuar: https://learn.microsoft.com/cli/azure/install-azure-cli"

az account show >/dev/null 2>&1 || fail "No hay sesión iniciada. Corre 'az login' primero."

if [[ -n "$SUBSCRIPTION" ]]; then
  az account set --subscription "$SUBSCRIPTION" || fail "No se pudo cambiar a la suscripción '$SUBSCRIPTION'. ¿El nombre/id es correcto y tienes acceso?"
fi

SUB_ID=$(az account show --query id -o tsv)
SUB_NAME=$(az account show --query name -o tsv)
log "Suscripción activa: $SUB_NAME ($SUB_ID)"

SPENDING_LIMIT=$(az rest --method get \
  --url "https://management.azure.com/subscriptions/${SUB_ID}?api-version=2022-12-01" \
  --query "subscriptionPolicies.spendingLimit" -o tsv 2>&1) \
  || fail "No se pudo consultar subscriptionPolicies vía 'az rest'. Respuesta: $SPENDING_LIMIT"

if [[ -z "$SPENDING_LIMIT" ]]; then
  log "Esta suscripción no reporta 'spendingLimit' — probablemente NO es de un tipo con"
  log "crédito (Free Trial / Azure for Students / crédito de Visual Studio), sino"
  log "Pay-As-You-Go u otra sin límite de gasto disponible en Azure."
  log "Para este tipo de suscripción NO existe un corte automático: la única red de"
  log "seguridad es la alerta de presupuesto de 1 USD (aviso por email, no bloquea"
  log "gasto). Si es intencional, continúa bajo tu propio criterio; deploy.sh no debe"
  log "tratar esto como un abort automático, pero sí debe mostrarte este mensaje."
  exit 3
fi

case "$SPENDING_LIMIT" in
  On)
    log "Límite de gasto: ON. Protegido — los recursos se desactivan solos si se agota el crédito del período."
    exit 0
    ;;
  Off|CurrentPeriodOff)
    MSG="$(cat <<EOF
Límite de gasto: $SPENDING_LIMIT (NO protegido).
Actívalo antes de desplegar nada:
  1. Portal de Azure -> Cost Management + Billing -> Suscripciones
  2. Selecciona '$SUB_NAME'
  3. En el panel de la suscripción, activa 'Límite de gasto' / 'Spending limit'
No existe API de escritura para esto — es solo desde el portal.
EOF
)"
    if [[ "$ALLOW_OFF" == "1" ]]; then
      log "AVISO (continuando por --allow-off): $MSG"
      exit 0
    fi
    fail "$MSG"
    ;;
  *)
    fail "Valor de spendingLimit no reconocido: '$SPENDING_LIMIT'. Revisa manualmente en el portal antes de continuar."
    ;;
esac
