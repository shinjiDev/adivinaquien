# Fase 0 -- red de seguridad financiera. Verifica que el "limite de gasto" de la
# suscripcion este prendido ANTES de dejar correr cualquier despliegue. Este limite es
# lo unico que corta servicios automaticamente si algo sale mal (a diferencia del budget
# de Bicep, que solo avisa por email); solo existe en suscripciones con credito (Free
# Trial, Azure for Students, credito de Visual Studio) y SOLO se puede prender/apagar
# desde el portal -- no hay API de escritura para esto, por eso este script solo lee y
# aborta, nunca "arregla" el estado por su cuenta.
#
# Campo verificado contra la doc oficial de la REST API "Subscriptions - Get"
# (subscriptionPolicies.spendingLimit, enum On|Off|CurrentPeriodOff) -- no existe
# subcomando dedicado de `az account`/`az billing` para esto, asi que se consulta con
# `az rest` directo contra el Resource Manager.
#
# Uso: .\preflight-budget-check.ps1 [-Subscription <id-o-nombre>]
# Salida: exit 0 si el limite esta On. Distinto de cero en cualquier otro caso (Off,
# CurrentPeriodOff, no aplica al tipo de suscripcion, o no se pudo verificar) -- el
# llamador (deploy.ps1) debe tratar cualquier salida no-cero como "no continuar".

param(
    [string]$Subscription = ""
)

$ErrorActionPreference = 'Stop'

function Write-Log($msg) { Write-Host "[preflight] $msg" }
function Fail($msg) {
    Write-Host "[preflight] ERROR: $msg" -ForegroundColor Red
    exit 1
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Fail "Azure CLI no esta instalado. Instalalo antes de continuar: https://learn.microsoft.com/cli/azure/install-azure-cli"
}

try {
    az account show *> $null
} catch {
    Fail "No hay sesion iniciada. Corre 'az login' primero."
}
if ($LASTEXITCODE -ne 0) {
    Fail "No hay sesion iniciada. Corre 'az login' primero."
}

if ($Subscription -ne "") {
    az account set --subscription $Subscription
    if ($LASTEXITCODE -ne 0) {
        Fail "No se pudo cambiar a la suscripcion '$Subscription'. ¿El nombre/id es correcto y tienes acceso?"
    }
}

$SubId = az account show --query id -o tsv
$SubName = az account show --query name -o tsv
Write-Log "Suscripcion activa: $SubName ($SubId)"

$SpendingLimit = az rest --method get `
    --url "https://management.azure.com/subscriptions/$SubId`?api-version=2022-12-01" `
    --query "subscriptionPolicies.spendingLimit" -o tsv 2>&1

if ($LASTEXITCODE -ne 0) {
    Fail "No se pudo consultar subscriptionPolicies via 'az rest'. Respuesta: $SpendingLimit"
}

if ([string]::IsNullOrWhiteSpace($SpendingLimit)) {
    Write-Log "Esta suscripcion no reporta 'spendingLimit' -- probablemente NO es de un tipo con"
    Write-Log "credito (Free Trial / Azure for Students / credito de Visual Studio), sino"
    Write-Log "Pay-As-You-Go u otra sin limite de gasto disponible en Azure."
    Write-Log "Para este tipo de suscripcion NO existe un corte automatico: la unica red de"
    Write-Log "seguridad es la alerta de presupuesto de 1 USD (aviso por email, no bloquea"
    Write-Log "gasto). Si es intencional, continua bajo tu propio criterio; deploy.ps1 no debe"
    Write-Log "tratar esto como un abort automatico, pero si debe mostrarte este mensaje."
    exit 3
}

switch ($SpendingLimit.Trim()) {
    "On" {
        Write-Log "Limite de gasto: ON. Protegido -- los recursos se desactivan solos si se agota el credito del periodo."
        exit 0
    }
    { $_ -in "Off", "CurrentPeriodOff" } {
        Fail @"
Limite de gasto: $SpendingLimit (NO protegido).
Activalo antes de desplegar nada:
  1. Portal de Azure -> Cost Management + Billing -> Suscripciones
  2. Selecciona '$SubName'
  3. En el panel de la suscripcion, activa 'Limite de gasto' / 'Spending limit'
No existe API de escritura para esto -- es solo desde el portal.
"@
    }
    default {
        Fail "Valor de spendingLimit no reconocido: '$SpendingLimit'. Revisa manualmente en el portal antes de continuar."
    }
}
