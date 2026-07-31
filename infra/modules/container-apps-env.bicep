// Sin workspace de Log Analytics dedicado a propósito (ver Fase 0 del plan): un
// workspace de Log Analytics tiene su propio nivel gratuito separado y complica el
// cálculo de costo cero. La API rechaza el string literal 'none' para
// appLogsConfiguration.destination (error real, descubierto vía `az deployment group
// validate`: "Supported values: 'log-analytics', 'azure-monitor' or none" — ese último
// "none" describe omitir la propiedad, no un valor string a pasar). Por eso acá
// simplemente no se declara `appLogsConfiguration`: sigue permitiendo ver logs de
// consola en vivo con `az containerapp logs show`, solo no los retiene a largo plazo.

@description('Nombre del Container Apps Environment.')
param environmentName string

@description('Región de despliegue.')
param location string

@description('Tags comunes del despliegue.')
param tags object = {}

resource environment 'Microsoft.App/managedEnvironments@2026-01-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {}
}

output environmentId string = environment.id
