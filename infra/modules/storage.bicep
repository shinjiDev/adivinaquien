// Storage account para AdivinaQue: Table Storage (IGameStore de producción, ver Fase 1
// — TableStorageGameStore) y un contenedor Blob para las claves de Data Protection
// (también Fase 1). Sin SQLite sobre Azure Files a propósito: el bloqueo de archivos de
// SQLite sobre SMB es problemático bajo concurrencia — ver plan de despliegue.
//
// allowSharedKeyAccess: false porque el código (Fase 1) autentica exclusivamente con
// managed identity — no hay ninguna ruta de código que use la clave de cuenta, así que
// deshabilitarla es una capa extra de seguridad sin costo funcional.

@description('Nombre de la storage account. Global, único, 3-24 caracteres, minúsculas/números.')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('Región de despliegue (debe coincidir con el resource group).')
param location string

@description('Tags comunes del despliegue.')
param tags object = {}

resource storageAccount 'Microsoft.Storage/storageAccounts@2026-04-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowSharedKeyAccess: false
    publicNetworkAccess: 'Enabled'
    accessTier: 'Hot'
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2026-04-01' = {
  parent: storageAccount
  name: 'default'
}

// Nombre fijo: TableStorageGameStore.cs asume esta tabla específica.
resource roomsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2026-04-01' = {
  parent: tableService
  name: 'rooms'
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2026-04-01' = {
  parent: storageAccount
  name: 'default'
}

// Nombre fijo: CreateDataProtectionBlobClient en Program.cs asume este contenedor.
resource dataProtectionContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2026-04-01' = {
  parent: blobService
  name: 'dataprotection-keys'
  properties: {
    publicAccess: 'None'
  }
}

output storageAccountId string = storageAccount.id
output storageAccountName string = storageAccount.name
output tableEndpoint string = storageAccount.properties.primaryEndpoints.table
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob
