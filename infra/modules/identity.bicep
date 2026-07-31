// Identidad administrada asignada por el usuario, sin secretos: el Container App se
// autentica contra Storage exclusivamente vía este identity, nunca con una
// connection string ni una clave de cuenta (ver storage.bicep: allowSharedKeyAccess:
// false).
//
// Dos asignaciones de rol, no una. El documento de despliegue original solo pedía
// "Storage Table Data Contributor" (para IGameStore). La segunda —Storage Blob Data
// Contributor— es un descubrimiento real de la Fase 1 de implementación: Data
// Protection (las claves que cifran las cookies/tokens de sesión de SignalR entre
// reinicios) persiste en un contenedor Blob, no en Table, así que sin este rol el
// Container App no podría leer/escribir esas claves y cada scale-to-zero → scale-up
// invalidaría las sesiones activas.

@description('Nombre de la identidad administrada.')
param identityName string

@description('Región (debe coincidir con el resource group).')
param location string

@description('Nombre de la storage account sobre la que se otorgan los roles (mismo resource group).')
param storageAccountName string

@description('Tags comunes del despliegue.')
param tags object = {}

var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: identityName
  location: location
  tags: tags
}

resource storageAccountRef 'Microsoft.Storage/storageAccounts@2026-04-01' existing = {
  name: storageAccountName
}

resource tableRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountRef.id, identity.id, storageTableDataContributorRoleId)
  scope: storageAccountRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributorRoleId)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource blobRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountRef.id, identity.id, storageBlobDataContributorRoleId)
  scope: storageAccountRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output identityId string = identity.id
output identityClientId string = identity.properties.clientId
output identityPrincipalId string = identity.properties.principalId
