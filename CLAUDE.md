# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Estado actual del repo

**Fases 0-6 completas — spec de `docs/PROMPT-claude-code.md` cumplida de punta a punta.**
`AdivinaQue.Engine` implementa la máquina de estados completa; `AdivinaQue.Contracts`/
`AdivinaQue.PackTool` tienen el esquema y validador del content pack; `AdivinaQue.Server`
tiene el `GameHub` de SignalR, gestión de salas con capacidad 2 atómica, `IGameStore`
(InMemory + Sqlite), QR, y un barrido de fondo que detecta timeouts sin que ningún
cliente llame a nada. `AdivinaQue.Client` (Blazor WASM) tiene las 4 pantallas completas
(inicio, sala de espera con QR, tablero, fin de partida) — verificado jugando una
partida completa de punta a punta con dos contextos de navegador (Playwright), incluyendo
reconexión y resync. `content/bailes-chile/pack.json` es un **borrador** de 24 cartas
(pasa el validador, pendiente de verificación humana de los hechos folclóricos — ver
`content/bailes-chile/MATRIZ-VERIFICACION.md`) contra el que el Client todavía no está
conectado (sigue sirviendo el mazo placeholder de 16 cartas; conectar un pack real al
Server es trabajo de infraestructura nuevo, no pedido explícitamente por ninguna fase).
`docker-compose.yml` levanta el stack completo con Sqlite persistido en volumen —
build y `docker compose up` verificados de punta a punta (imagen construida, contenedor
sirviendo `/health` y `/` en 200, variables de entorno y volumen confirmados dentro del
contenedor). 103 tests en verde en total (Engine + PackTool + Server, este último con
integración real de dos clientes SignalR; el Client no suma un proyecto de tests de
componentes, ver plan de Fase 4). Antes de escribir código de producto sobre este
repo, revisa `docs/PROMPT-claude-code.md` para el contexto histórico de cada fase — el
trabajo se ejecutó **fase por fase, deteniéndose al final de cada una** para mostrar un
resumen y esperar aprobación antes de continuar; cualquier trabajo nuevo a partir de acá
ya no tiene fases predefinidas en ese documento.

## Comandos

```
dotnet build                                          # build de toda la solución (AdivinaQue.slnx)
dotnet test                                           # todos los tests
dotnet test tests/AdivinaQue.Engine.Tests             # solo el motor puro
dotnet test tests/AdivinaQue.PackTool.Tests           # solo el validador de packs
dotnet test tests/AdivinaQue.Server.Tests             # solo integración de servidor
dotnet test --filter "FullyQualifiedName~NombreTest"  # un test puntual
dotnet run --project src/AdivinaQue.Server            # levanta servidor + Blazor WASM (hosted)
dotnet run --project src/AdivinaQue.PackTool -- validate <ruta-a-pack.json>  # valida un pack y emite reporte
docker build .                                        # imagen multi-stage (pesado: instala workload wasm-tools)
docker compose up                                     # entorno local completo: server + Sqlite persistido, puerto 5299 (override: ADIVINAQUE_PORT)
```

Detalle de variables de entorno y qué proveedores de hosting sirven (y cuáles rompen
WebSockets por dormir el contenedor) vive en `README.md`, no acá, para no duplicar.

**Nota del Dockerfile:** la imagen base del SDK no trae `python`, que el paso de
compilación nativa de emscripten (WASM) exige al publicar en Release — sin
`python-is-python3` instalado, `dotnet publish` falla recién al final del build (tras
instalar todo el workload `wasm-tools`) con "unable to find python in $PATH".

El archivo de solución es `AdivinaQue.slnx` (formato XML nuevo, no el `.sln` clásico) —
`dotnet build`/`dotnet test` lo detectan solos sin pasar la ruta.

El Server lee `Storage:Provider` (`InMemory`|`Sqlite`), `Match:AnswerTimeoutSeconds`,
`Match:DisconnectGraceSeconds`, `Match:WrongGuessPolicy`, `Room:TtlMinutes`,
`Room:SweepIntervalSeconds` desde `appsettings.json` (todo sobreescribible por variable
de entorno, p. ej. `Storage__Provider=Sqlite`).

## Stack (no negociable)

- Servidor: ASP.NET Core 8, SignalR, Minimal APIs
- Cliente: Blazor WebAssembly
- `AdivinaQue.Contracts`: proyecto compartido servidor/cliente para DTOs y nombres de
  eventos — nunca strings mágicos dispersos, todo vive ahí como constantes o enum
- Tests: xUnit + FluentAssertions
- Persistencia: abstracción `IGameStore` con `InMemoryGameStore` y `SqliteGameStore`,
  seleccionable por configuración (el estado nunca puede depender solo de memoria de
  proceso en producción, porque el hosting final aún no está decidido)
- QR: `QRCoder`, generado en el servidor (el QR codifica la URL de deep link completa,
  `https://host/join/ABC123`, no solo el código)
- Dockerfile multi-stage desde el día 1, todo configurable por variables de entorno, sin
  atarse a un proveedor de hosting específico

## Estructura de proyecto

```
AdivinaQue.slnx
Directory.Build.props        # TargetFramework=net8.0, Nullable, ImplicitUsings, LangVersion — compartido, no lo repitas por csproj
NuGet.Config                 # fuentes limitadas a nuget.org, ver nota abajo
Dockerfile / .dockerignore
src/
├── AdivinaQue.Contracts/   # DTOs, eventos, enums, códigos de error (Realtime/) + esquema de content pack (ContentPack/)
├── AdivinaQue.Engine/      # motor puro: sin red, sin I/O, sin DateTime.Now (implementado en Fase 1)
├── AdivinaQue.Server/      # Rooms/, Persistence/, Hubs/, BackgroundServices/, Mapping/, Qr/ (ver realtime-contract)
├── AdivinaQue.Client/      # Blazor WASM: Pages/ (Home, Join, Room), Components/ (WaitingRoomView,
│                           # GameBoardView, GameOverView, CardTile, ConnectionStatusBadge),
│                           # Services/ (GameClient, PlayerIdentity), wwwroot/js/interop.js (localStorage)
└── AdivinaQue.PackTool/    # CLI que valida mazos y emite reporte (Model/, Analysis/, Validation/, Reporting/)
tests/
├── AdivinaQue.Engine.Tests/    # xUnit + FluentAssertions, referencia Engine
├── AdivinaQue.PackTool.Tests/  # xUnit + FluentAssertions, referencia Contracts + PackTool (agregado en Fase 2)
└── AdivinaQue.Server.Tests/    # xUnit + FluentAssertions + WebApplicationFactory, referencia Server
content/<pack-id>/pack.json    # aparece en Fase 5 (mazo de bailes típicos de Chile)
```

El esquema del content pack (`AdivinaQue.Contracts/ContentPack/`) vive en `Contracts`,
no en `PackTool`, porque Server (Fase 3+) y Client (Fase 4) también van a necesitar leer
el mismo pack. `PackTool` solo agrega la lógica de validación/reporte y el CLI.

Detalle de convenciones de estructura, DI y estilo de tests: skill `stack-conventions`.

**Nota de portabilidad:** el `NuGet.Config` de la raíz limita las fuentes a `nuget.org`
porque el `NuGet.Config` global de esta máquina traía feeds corporativos de otro
proyecto que no resuelven DNS y rompían el restore de paquetes nuevos (pasó al
scaffolder Blazor WASM). Si `dotnet restore` falla con `NU1301` en una fuente
desconocida, es este problema, no el repo.

## Arquitectura: por qué está separado así

**El motor no sabe nada del tema del mazo.** `AdivinaQue.Engine` opera sobre cartas como
vectores de atributos y preguntas que los particionan — nunca sabe qué es un "baile" o
un "traje". Cambiar de temática es reemplazar el JSON del pack, nada más. El esquema y
las 8 reglas de validación de un pack están en la skill `content-pack`
(`.claude/skills/content-pack/SKILL.md`); consúltala siempre que se toque contenido,
cartas, atributos o el validador.

**Motor puro y determinista.** `AdivinaQue.Engine` no referencia ASP.NET, no usa
`DateTime.Now` (inyecta `IClock`) ni `Random` sin semilla (inyecta `ISeededRandom`). Toda
la lógica de reglas (máquina de estados, validación de acciones, condiciones de victoria)
vive ahí y debe ser testeable sin levantar servidor. El Hub de SignalR solo traduce
mensajes y aplica autorización — no contiene lógica de juego.

**Autoridad del servidor.** La carta secreta de cada jugador vive solo en el servidor y
jamás se serializa hacia el cliente del oponente. Lo que se envía a cada cliente es una
proyección personalizada por jugador, no el estado completo. Cualquier cambio en la
serialización de estado debe mantener un test que falle si la carta secreta aparece en
el payload del oponente.

**Identidad ≠ conexión.** Cada jugador tiene un `PlayerId` (GUID) persistido en
`localStorage`, distinto del `ConnectionId` de SignalR (efímero, cambia en cada
reconexión). Nunca uses `ConnectionId` como identidad. Reconectar es "el mismo
`PlayerId` se reasocia a la sala" y dispara un resync completo.

**Versionado e idempotencia.** Cada mutación de estado incrementa `StateVersion` (long
monotónico), dentro de la proyección que viaja en `StateSync`/`GameStarted` — no en cada
evento liviano (`QuestionAsked`, `TurnEnded`, etc.; el Hub siempre manda un `StateSync`
justo después de cualquier mutación exitosa, así que el cliente nunca depende solo de la
señal liviana). Si el cliente detecta un salto, llama `RequestResync`. Cada acción del
cliente lleva su propia `ActionId` (GUID): reenviar la misma acción tras una reconexión
no debe aplicarla dos veces.

**Acciones inválidas nunca lanzan excepción hacia el cliente.** Se validan contra la
tupla `(Estado, Fase, ActorId)` y devuelven un `Error(code, message)` tipado sin alterar
el estado.

**Cada componente Blazor que lee estado de `GameClient` se suscribe él mismo a
`Changed`.** No alcanza con que un componente ancestro (p. ej. `Room.razor`) se suscriba
y llame `StateHasChanged()`: si el componente hijo tiene su propio render en curso
disparado por un evento local (p. ej. un botón con un `async` handler todavía
esperando), la re-renderización en cascada del padre puede perderse y el hijo queda
mostrando datos viejos aunque el dato ya haya llegado — un bug real encontrado en Fase 4
(el respondedor no veía la pregunta pendiente pese a que el `ProjectionDto` correcto ya
estaba en memoria). Cada vista que lee `GameClient.Projection`/`RoomCode`/etc. debe
implementar `IDisposable`, suscribirse en `OnInitialized` y desuscribirse en `Dispose`
(ver `GameBoardView`, `WaitingRoomView`, `GameOverView`, `ConnectionStatusBadge`).

## Máquina de estados, roles de turno y contrato de eventos

El detalle completo (estados/fases, acciones legales, roles PREGUNTADOR/RESPONDEDOR,
"No aplica", timeouts, y el contrato de eventos SignalR cliente↔servidor) vive en dos
skills, no aquí, para no mantener dos copias de la misma verdad:

- `game-rules` — máquina de estados, fases de turno, condiciones de victoria.
- `realtime-contract` — eventos, `StateVersion`/`ActionId`, salas, códigos, reconexión.

Consúltalas al tocar `AdivinaQue.Engine` o el `GameHub` respectivamente.

## Anti-objetivos (no construir)

Sin cuentas de usuario ni login. Sin matchmaking ni salas públicas. Sin chat de texto.
Sin espectadores. Sin más de 2 jugadores. Sin microservicios, sin ORM pesado, sin event
sourcing. Sin librería de estado del lado cliente más allá de lo que trae Blazor.

## Skills en `.claude/skills/`

- `content-pack` — esquema del pack, receta de dimensionamiento de atributos/preguntas,
  y las 8 reglas de validación (R1–R8) que `AdivinaQue.PackTool` debe implementar.
- `game-rules` — máquina de estados y reglas del motor (ver arriba).
- `realtime-contract` — contrato de eventos SignalR completo (ver arriba).
- `stack-conventions` — estructura de proyectos, referencias, config compartida, DI y
  estilo de tests.

## Cómo trabajar en este repo

Antes de escribir código de una fase, muestra el plan y las decisiones de diseño a
tomar. Si una instrucción de `docs/PROMPT-claude-code.md` choca con algo descubierto
durante la implementación, dilo explícitamente en vez de resolverlo en silencio.
Preferencia declarada: menos código y más tests sobre las reglas del motor.
