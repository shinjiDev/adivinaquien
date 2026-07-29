# AdivinaQue

"Adivina Quién" para 2 jugadores, jugado desde el navegador, con mazos de contenido
intercambiables (content packs) — el motor de juego nunca sabe de qué trata el mazo.

## Requisitos

- .NET 8 SDK
- Para compilar el cliente Blazor WebAssembly localmente (fuera de Docker): workload
  `wasm-tools` (`dotnet workload install wasm-tools`)
- Docker + Docker Compose, solo si vas a levantar el entorno contenedorizado

## Desarrollo local

```
dotnet build                                          # build de toda la solución
dotnet test                                           # todos los tests (Engine + PackTool + Server)
dotnet test --filter "FullyQualifiedName~NombreTest"  # un test puntual
dotnet run --project src/AdivinaQue.Server            # levanta servidor + cliente Blazor (hosted), http://localhost:5299 por defecto
```

El archivo de solución es `AdivinaQue.slnx`; `dotnet build`/`dotnet test` lo detectan
solos, sin pasar la ruta.

Si `dotnet restore` falla con `NU1301` en una fuente desconocida: el `NuGet.Config` de
la raíz limita las fuentes a `nuget.org` a propósito, por si el `NuGet.Config` global de
tu máquina trae otros feeds que no resuelven DNS.

## Validar un content pack

```
dotnet run --project src/AdivinaQue.PackTool -- validate <ruta-a-pack.json>
```

Corre las 8 reglas de validación (tamaño de mazo, unicidad de cartas, entropía por
pregunta, redundancia, profundidad del árbol de decisión, etc.) y emite un reporte. Ver
`content/bailes-chile/pack.json` para un ejemplo (marcado como borrador) y la skill
`content-pack` para el detalle de cada regla.

## Docker

```
docker build .                 # imagen multi-stage (pesado la primera vez: instala el workload wasm-tools)
docker compose up              # entorno local completo: servidor + cliente + Sqlite persistido en volumen
docker compose up --build      # reconstruye la imagen antes de levantar
```

`docker-compose.yml` levanta un único servicio (`server`), con `Storage__Provider=Sqlite`
y la base de datos en un volumen nombrado (`adivinaque-data`) para que las salas
sobrevivan un reinicio del contenedor. Es la configuración más cercana a producción que
se puede probar 100% en local.

Publica en el puerto `5299` del host por defecto. Si ese puerto ya está ocupado (u otro
`docker compose` de otro proyecto ya lo usa), sobreescríbelo sin tocar el archivo:

```
ADIVINAQUE_PORT=8090 docker compose up
```

## Variables de entorno

Todo lo de `appsettings.json` es sobreescribible por variable de entorno (doble guion
bajo `__` como separador de sección, convención estándar de ASP.NET Core):

| Variable | Valores | Default | Qué hace |
|---|---|---|---|
| `Storage__Provider` | `InMemory` \| `Sqlite` | `InMemory` | Backend de persistencia de salas. `InMemory` no sobrevive un reinicio del proceso — solo sirve para desarrollo. |
| `Storage__SqliteConnectionString` | connection string de SQLite | `Data Source=adivinaque.db` | Solo aplica si `Storage__Provider=Sqlite`. |
| `Match__AnswerTimeoutSeconds` | entero | `60` | Segundos para responder una pregunta antes de que el motor la marque expirada. |
| `Match__DisconnectGraceSeconds` | entero | `120` | Segundos de gracia tras una desconexión antes de dar la partida por abandonada. |
| `Match__WrongGuessPolicy` | `EndsMatch` | `EndsMatch` | Qué pasa si un jugador adivina mal. |
| `Room__TtlMinutes` | entero | `30` | Minutos de inactividad antes de que el barrido de fondo elimine una sala. |
| `Room__SweepIntervalSeconds` | entero | `1` | Frecuencia del barrido de fondo que detecta timeouts. |

## Despliegue: qué proveedores sirven y cuáles no

Esta aplicación mantiene **estado de partida en el proceso del servidor** (en memoria, o
en SQLite si `Storage__Provider=Sqlite`) y una conexión SignalR (WebSocket) persistente
por jugador durante toda la partida. Eso impone dos restricciones no negociables al
elegir dónde desplegar:

**❌ No sirven los free tiers que duermen el contenedor por inactividad** (scale-to-zero
o "cold start" tras N minutos sin tráfico HTTP). Cuando el proveedor pone a dormir o
recicla la instancia, la conexión WebSocket se corta a mitad de partida sin ningún aviso
al motor de juego, y si además el contenedor perdió su disco efímero, `SqliteGameStore`
pierde el estado con él. Esto rompe partidas en curso de forma silenciosa e
irrecuperable. Ejemplos típicos de esta trampa: planes gratuitos de Render, Railway y
similares que "duermen" el servicio tras un rato sin requests, o cualquier plataforma
serverless (Cloud Run, Lambda, Azure Container Apps con `minReplicas: 0`) configurada
para escalar a cero.

**❌ No corras más de una réplica/instancia detrás de un balanceador de carga.** El
proyecto no implementa un backplane de SignalR (p. ej. Redis), a propósito — es un
anti-objetivo del spec ("sin microservicios"). Con más de una instancia, un jugador
puede terminar conectado a una réplica distinta a la de su rival y nunca recibir sus
eventos. Esta app está diseñada para correr como **una sola instancia siempre viva**.

**✅ Lo que sí funciona:** cualquier hosting que garantice el proceso corriendo de forma
continua, sin dormir por inactividad:
- Una VPS pequeña y siempre encendida (DigitalOcean, Linode, Vultr, Hetzner) corriendo
  `docker compose up -d`.
- Fly.io, configurado explícitamente con `min_machines_running >= 1` (auto-stop
  deshabilitado) para esta app.
- El plan pago (no-sleep) de Render, Railway o similar.
- Cualquier proveedor de contenedores administrados con "réplicas mínimas = 1" y sin
  escalado automático a cero.

Si el proveedor termina la conexión TLS/HTTP antes de llegar al proceso (reverse proxy,
balanceador gestionado), asegúrate de que el proxy soporte upgrade a WebSocket y de
configurar el reenvío de cabeceras (`ASPNETCORE_FORWARDED_HEADERS_ENABLED=true` u
overrides de `Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersOptions`) para que
`request.Host`/`request.Scheme` sigan siendo correctos — el QR de cada sala codifica la
URL completa (`https://host/join/ABC123`) a partir de esos valores.

## Estructura y arquitectura

El detalle de arquitectura, convenciones de proyecto y la máquina de estados del juego
vive en `CLAUDE.md` y en las skills de `.claude/skills/` (`content-pack`, `game-rules`,
`realtime-contract`, `stack-conventions`) — son la fuente de verdad, no se duplica acá.
