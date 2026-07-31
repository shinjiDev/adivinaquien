// maxReplicas: 1 es INVARIANTE, no un default a ajustar más adelante. AdivinaQue no
// tiene backplane de SignalR (Redis u otro) — es un anti-objetivo explícito del spec
// del juego ("sin microservicios"). Con más de una réplica, los dos jugadores de una
// sala pueden terminar conectados a procesos distintos y dejar de verse entre ellos,
// porque el estado de la partida vive en memoria de un solo proceso (ver IGameStore:
// Table Storage lo persiste, pero las conexiones SignalR activas y el enrutamiento de
// eventos en tiempo real no se comparten entre réplicas). minReplicas: 0 es lo que
// hace posible el costo cero (ver README, sección "Despliegue: qué proveedores
// sirven"): cualquier proveedor no es apto salvo que garantice esta única instancia
// viva o duerma correctamente — Container Apps sí soporta scale-to-zero de forma
// nativa sin cortar el proceso a mitad de partida (el apagado ordenado de Fase 1
// notifica a las salas activas antes de terminar).

@description('Nombre del Container App.')
param containerAppName string

@description('Región de despliegue.')
param location string

@description('Tags comunes del despliegue.')
param tags object = {}

@description('Resource ID del Container Apps Environment.')
param environmentId string

@description('Resource ID de la identidad administrada asignada por el usuario.')
param identityId string

@description('Client ID de la misma identidad, pasado como env var para autenticar contra Storage sin secretos.')
param identityClientId string

@description('Endpoint del servicio Table de la storage account.')
param tableEndpoint string

@description('Endpoint del servicio Blob de la storage account (Data Protection keys).')
param blobEndpoint string

@description('Imagen completa (registro/repo:tag). PLACEHOLDER hasta Fase 3 — ver nota en main.bicep, no hay imagen real en ghcr.io todavía.')
param containerImage string

@description('Entorno ASP.NET Core.')
param aspnetEnvironment string = 'Production'

resource containerApp 'Microsoft.App/containerApps@2026-01-01' = {
  name: containerAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      // Sin bloque `registries`, a propósito: ese array es solo para registries
      // CREDENCIADOS (requiere username + passwordSecretRef, ver doc de Container
      // Apps "Container registries"). Un despliegue real con
      // `registries: [{ server: 'ghcr.io' }]` sin password falló con
      // ContainerAppRegistriesPasswordSecretRefNotFound porque ARM interpreta
      // cualquier entrada en `registries` como una que exige credenciales. Para una
      // imagen pública (nuestro caso) simplemente no se declara el registry — Container
      // Apps la pulea anónimamente igual, con solo `image` apuntando a ghcr.io.
    }
    template: {
      terminationGracePeriodSeconds: 30
      containers: [
        {
          name: 'adivinaquien-server'
          image: containerImage
          resources: {
            // 0.25 vCPU / 0.5 GiB: base del cálculo de 200 h/mes gratis (ver plan de
            // despliegue). Subir esto reduce las horas gratis proporcionalmente, no
            // lo cambies sin recalcular el presupuesto.
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
            { name: 'ASPNETCORE_ENVIRONMENT', value: aspnetEnvironment }
            { name: 'Storage__Provider', value: 'Table' }
            { name: 'Storage__TableEndpoint', value: tableEndpoint }
            { name: 'Storage__BlobEndpoint', value: blobEndpoint }
            { name: 'Storage__ManagedIdentityClientId', value: identityClientId }
            { name: 'ContentPack__RootDirectory', value: 'content' }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/healthz'
                port: 8080
              }
              initialDelaySeconds: 3
              periodSeconds: 5
              failureThreshold: 20
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/healthz'
                port: 8080
              }
              periodSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/healthz'
                port: 8080
              }
              periodSeconds: 30
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
output containerAppId string = containerApp.id
