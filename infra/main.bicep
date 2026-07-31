// Despliegue de AdivinaQue en Azure Container Apps, dentro del nivel gratuito.
// Fase 0: red de seguridad financiera (budget). Fase 2: infraestructura completa
// (container-apps-env, storage, identity, container-app), ensamblada acá.
//
// Se despliega SIEMPRE a nivel resource group (targetScope por defecto). El resource
// group mismo lo crea el script de despliegue (az group create), no este archivo — así
// az deployment group validate/what-if pueden correr sin ambigüedad de scope.

@description('Nombre corto del proyecto, usado como prefijo de nombres de recursos.')
param projectName string = 'adivinaquien'

@description('Entorno: prod, dev, etc. Se usa en tags y en el nombre del budget.')
param environment string = 'prod'

@description('Email(s) que reciben las alertas de presupuesto.')
param budgetContactEmails array

@description('Región de despliegue. Ver plan de Fase 0: brazilsouth recomendado por geografía, verificado por el preflight contra la suscripción real antes de desplegar.')
param location string = 'brazilsouth'

@description('Imagen completa del contenedor (registro/repo:tag). PLACEHOLDER hasta Fase 3 (todavía no existe un repo GitHub que publique a ghcr.io) — ver nota junto al módulo containerApp más abajo.')
param containerImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

// Tags comunes a TODO recurso de este despliegue — permiten filtrar el gasto real en
// Cost Analysis y le dicen a teardown.sh (Fase 3) qué borrar sin adivinar por nombre.
var commonTags = {
  proyecto: projectName
  entorno: environment
  'gestionado-por': 'bicep'
}

// Nombre de storage account: global, único, 3-24 caracteres, solo minúsculas/números.
// uniqueString(resourceGroup().id) da 13 caracteres estables (mismo resource group ⇒
// mismo nombre en cada redeploy, así el script de despliegue es idempotente sin tener
// que guardar el nombre en ningún lado).
var storageAccountName = toLower('${take(projectName, 6)}${environment}${uniqueString(resourceGroup().id)}')

module budget 'modules/budget.bicep' = {
  name: 'budget-deployment'
  params: {
    budgetName: '${projectName}-${environment}-budget'
    amount: 1
    contactEmails: budgetContactEmails
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    storageAccountName: storageAccountName
    location: location
    tags: commonTags
  }
}

module identity 'modules/identity.bicep' = {
  name: 'identity-deployment'
  params: {
    identityName: '${projectName}-${environment}-identity'
    location: location
    storageAccountName: storage.outputs.storageAccountName
    tags: commonTags
  }
}

module containerAppsEnv 'modules/container-apps-env.bicep' = {
  name: 'container-apps-env-deployment'
  params: {
    environmentName: '${projectName}-${environment}-env'
    location: location
    tags: commonTags
  }
}

// NOTA — discrepancia con el documento original: el documento sugería la env var
// `GameStore__Provider=Table`, pero la clave real implementada en Program.cs (Fase 1)
// es `Storage__Provider` (así se llama la sección en appsettings.json y así la lee la
// factory de IGameStore). container-app.bicep usa la clave real, no la del documento —
// señalado explícitamente en vez de resuelto en silencio, tal como pide el documento.
//
// NOTA — sobre containerImage: no existe todavía ninguna imagen real publicada en
// ghcr.io (Fase 3 crea el repo GitHub y el pipeline de publish). El valor default de
// arriba es una imagen pública de ejemplo de Microsoft, solo para que `what-if` y una
// primera aplicación de infraestructura no fallen por falta de imagen — se reemplaza
// por la imagen real de AdivinaQue en el primer `deploy.sh` de la Fase 3, pasando
// -containerImage explícito. Desplegar este módulo ahora con el placeholder es seguro
// (crea el Container App, que arrancará el sample de Microsoft y no responderá /healthz
// correctamente hasta el redeploy con la imagen real) pero no es la app final.
module containerApp 'modules/container-app.bicep' = {
  name: 'container-app-deployment'
  params: {
    containerAppName: '${projectName}-${environment}-app'
    location: location
    tags: commonTags
    environmentId: containerAppsEnv.outputs.environmentId
    identityId: identity.outputs.identityId
    identityClientId: identity.outputs.identityClientId
    tableEndpoint: storage.outputs.tableEndpoint
    blobEndpoint: storage.outputs.blobEndpoint
    containerImage: containerImage
  }
}

output budgetId string = budget.outputs.budgetId
output commonTagsUsed object = commonTags
output storageAccountName string = storage.outputs.storageAccountName
output identityClientId string = identity.outputs.identityClientId
output containerAppFqdn string = containerApp.outputs.fqdn
