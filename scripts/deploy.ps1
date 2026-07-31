# Equivalente PowerShell de deploy.sh. Reutiliza preflight.sh y smoke-test.sh vía bash
# (Git para Windows) en vez de duplicar esa lógica acá — evita mantener dos copias del
# mismo chequeo de límite de gasto/providers/WebSocket que puedan divergir.
#
# Uso:
#   .\deploy.ps1 -Subscription <id-o-nombre> [opciones]

param(
    [Parameter(Mandatory = $true)]
    [string]$Subscription,

    [string]$ResourceGroup = "rg-adivinaquien-prod",
    [string]$Location = "brazilsouth",
    [string]$ProjectName = "adivinaquien",
    [string]$Environment = "prod",
    [string]$ImageTag,
    [switch]$SkipBuild,
    [switch]$Yes
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir

function Log($msg) { Write-Host "[deploy] $msg" }
function Fail($msg) { Write-Error "[deploy] ERROR: $msg"; exit 1 }

$bashCmd = Get-Command bash -ErrorAction SilentlyContinue
if (-not $bashCmd) {
    Fail "No se encontró 'bash' en el PATH (Git para Windows lo instala). Se necesita para preflight.sh y smoke-test.sh."
}

Push-Location $RepoRoot
try {
    Log "=== 1/8 - Preflight ==="
    & bash "$ScriptDir/preflight.sh" --subscription $Subscription --location $Location
    if ($LASTEXITCODE -ne 0) { Fail "preflight.sh abortó (código $LASTEXITCODE)." }

    az account set --subscription $Subscription
    if ($LASTEXITCODE -ne 0) { Fail "No se pudo cambiar a la suscripción '$Subscription'." }

    Log "=== 2/8 - Resource group '$ResourceGroup' ==="
    az group create --name $ResourceGroup --location $Location `
        --tags "proyecto=$ProjectName" "entorno=$Environment" "gestionado-por=bicep" --output none
    if ($LASTEXITCODE -ne 0) { Fail "No se pudo crear/actualizar el resource group '$ResourceGroup'." }
    Log "   OK."

    $remoteUrl = git -C $RepoRoot remote get-url origin 2>$null
    if (-not $remoteUrl) { Fail "No hay remote 'origin' configurado - necesito owner/repo de GitHub para la imagen en ghcr.io." }
    $ownerRepo = ($remoteUrl -replace '^(git@|https://)github\.com[:/]', '' -replace '\.git$', '').ToLower()

    if (-not $ImageTag) { $ImageTag = (git -C $RepoRoot rev-parse --short HEAD).Trim() }
    $image = "ghcr.io/${ownerRepo}:${ImageTag}"
    Log "   Imagen: $image"

    if (-not $SkipBuild) {
        Log "=== 3/8 - Build + push de la imagen ==="
        docker build -t $image -t "ghcr.io/${ownerRepo}:latest" $RepoRoot
        if ($LASTEXITCODE -ne 0) { Fail "docker build falló." }
        docker push $image
        if ($LASTEXITCODE -ne 0) { Fail "docker push de '$image' falló. ¿'docker login ghcr.io' está vigente?" }
        docker push "ghcr.io/${ownerRepo}:latest"
        if ($LASTEXITCODE -ne 0) { Fail "docker push de ':latest' falló." }
        Log "   OK."
    } else {
        Log "=== 3/8 - Build + push (omitido por -SkipBuild) ==="
    }

    Log "=== 4/8 - az bicep build (sintaxis) ==="
    az bicep build --file "$RepoRoot/infra/main.bicep" --stdout | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "az bicep build encontró errores de sintaxis/esquema en main.bicep." }
    Log "   OK."

    $paramArgs = @(
        "--parameters", "$RepoRoot/infra/main.parameters.json",
        "--parameters", "containerImage=$image", "location=$Location", "projectName=$ProjectName", "environment=$Environment"
    )

    Log "=== 5/8 - az deployment group validate ==="
    az deployment group validate --resource-group $ResourceGroup --template-file "$RepoRoot/infra/main.bicep" @paramArgs --output none
    if ($LASTEXITCODE -ne 0) { Fail "az deployment group validate falló - ver el error de arriba." }
    Log "   OK."

    Log "=== 6/8 - az deployment group what-if ==="
    az deployment group what-if --resource-group $ResourceGroup --template-file "$RepoRoot/infra/main.bicep" @paramArgs --result-format ResourceIdOnly

    if (-not $Yes) {
        $confirm = Read-Host "¿Aplicar estos cambios contra la suscripción real? Escribí 'SI' para continuar"
        if ($confirm -ne "SI") { Fail "Cancelado por el usuario (no se escribió 'SI')." }
    }

    Log "=== 7/8 - az deployment group create ==="
    $deploymentName = "adivinaquien-deploy-$(Get-Date -Format 'yyyyMMddHHmmss')"
    $outputsJson = az deployment group create --name $deploymentName --resource-group $ResourceGroup `
        --template-file "$RepoRoot/infra/main.bicep" @paramArgs --query "properties.outputs" -o json
    if ($LASTEXITCODE -ne 0) { Fail "az deployment group create falló - la infraestructura previa (si existía) sigue intacta." }

    $outputs = $outputsJson | ConvertFrom-Json
    $containerAppFqdn = $outputs.containerAppFqdn.value
    $storageAccountName = $outputs.storageAccountName.value
    $appUrl = "https://$containerAppFqdn"
    $containerAppName = "$ProjectName-$Environment-app"
    Log "   OK. URL: $appUrl"

    Log "=== 8/8 - Smoke test (con reintentos por arranque en frío) ==="
    $smokeOk = $false
    for ($attempt = 1; $attempt -le 6; $attempt++) {
        & bash "$ScriptDir/smoke-test.sh" --url $appUrl
        if ($LASTEXITCODE -eq 0) { $smokeOk = $true; break }
        Log "   Intento $attempt falló, reintentando en 20s (arranque en frío puede tardar)..."
        Start-Sleep -Seconds 20
    }

    if (-not $smokeOk) {
        Log "!!! Smoke test falló tras varios intentos. Intentando rollback a la revisión anterior..."
        $revisions = (az containerapp revision list --name $containerAppName --resource-group $ResourceGroup `
            --query "sort_by([], &properties.createdTime)[].name" -o tsv) -split "`n" | Where-Object { $_ }
        if ($revisions.Count -lt 2) {
            Fail "No hay una revisión anterior a la cual volver (este era el primer despliegue). Revisa los logs con 'az containerapp logs show --name $containerAppName --resource-group $ResourceGroup'."
        }
        $previousRevision = $revisions[-2]
        az containerapp revision activate --revision $previousRevision --resource-group $ResourceGroup --output none
        az containerapp ingress traffic set --name $containerAppName --resource-group $ResourceGroup `
            --revision-weight "$previousRevision=100" --output none
        Fail "Rollback aplicado a la revisión '$previousRevision'. El despliegue nuevo NO quedó activo - revisa la imagen '$image' antes de reintentar."
    }

    Log ""
    Log "=========================================="
    Log " Despliegue OK"
    Log " URL:               $appUrl"
    Log " Resource group:    $ResourceGroup"
    Log " Storage account:   $storageAccountName"
    Log " Imagen desplegada: $image"
    Log "=========================================="
} finally {
    Pop-Location
}
