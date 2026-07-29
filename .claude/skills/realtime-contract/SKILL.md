---
name: realtime-contract
description: Contrato de eventos SignalR completo (cliente→servidor y servidor→cliente), identidad de jugador, versionado de estado (StateVersion), idempotencia (ActionId), salas y reconexión. Actívala al tocar el GameHub, al agregar o modificar un evento, al implementar reconexión/resync en el cliente, o al decidir qué se proyecta en el estado enviado a cada jugador.
---

# Contrato de tiempo real

Fuente completa: `docs/PROMPT-claude-code.md`. Este documento cubre la capa de red
(`AdivinaQue.Server` ↔ `AdivinaQue.Client` vía SignalR); las reglas de qué transición de
estado es válida viven en la skill `game-rules`.

## Regla de oro: todo evento y nombre vive en `Contracts`

Nunca strings mágicos dispersos en Hub o cliente. Cada nombre de evento, DTO, enum y
código de error se define una vez en `AdivinaQue.Contracts` y se referencia desde ambos
lados, para que no puedan divergir.

## Identidad ≠ conexión

- `PlayerId` (GUID): identidad estable, persistida en `localStorage` del navegador.
- `ConnectionId` de SignalR: efímero, cambia en cada reconexión. **Nunca se usa como
  identidad.**
- Reconectar = "el mismo `PlayerId` se reasocia a la sala" → dispara un resync completo
  (`StateSync`), no un evento especial de "login".

## Autoridad del servidor y proyección

La carta secreta de cada jugador vive solo en el servidor y **jamás se serializa hacia
el cliente del oponente**. Lo que recibe cada cliente es una proyección personalizada
por jugador (su propia carta sí, la del oponente no), nunca el estado completo
compartido. Cualquier cambio a la serialización de estado debe mantener un test que
falle si la carta secreta aparece en el payload del oponente.

## Versionado e idempotencia

- `StateVersion` (long monotónico): se incrementa en cada mutación de estado. Todo
  evento servidor→cliente lo incluye. Si el cliente detecta un salto, llama
  `RequestResync`.
- `ActionId` (GUID): cada acción del cliente lleva la suya. Reenviar la misma acción tras
  una reconexión no debe aplicarla dos veces — el servidor debe poder detectar y
  descartar duplicados por `ActionId` ya procesado.

## Eventos

**Cliente → Servidor:** `CreateRoom(playerId)`, `JoinRoom(code, playerId)`, `SetReady`,
`AskQuestion(actionId, text, suggestedFrom?)`,
`SubmitAnswer(actionId, Yes|No|NotApplicable)`, `ToggleElimination(cardId)`,
`EndTurn(actionId)`, `MakeGuess(actionId, cardId)`, `RequestResync`, `LeaveRoom`.

`text` es libre, máximo 200 caracteres, sanitizado antes de reenviarlo al otro cliente.
`suggestedFrom` (opcional) solo se rellena si la pregunta salió de un chip sugerido —
sirve para métricas, no cambia el flujo.

**Servidor → Cliente:** `RoomUpdated(code, playerIds)`, `GameStarted(ProjectionDto)`,
`StateSync(ProjectionDto)`, `QuestionAsked(text)`, `AnswerGiven(answer)`,
`QuestionExpired`, `TurnEnded`, `OpponentDisconnected(secondsRemaining)`,
`OpponentReconnected`, `GameOver`, `Error(ErrorDto)`.

`StateVersion` va **dentro** de `ProjectionDto` (`StateSync`/`GameStarted`), no en cada
evento — `QuestionAsked`/`AnswerGiven`/`TurnEnded`/`GameOver`/`OpponentDisconnected`/
`OpponentReconnected`/`QuestionExpired` son señales livianas sin payload de estado; el
Hub siempre manda un `StateSync` inmediatamente después de cada mutación exitosa, así
que el cliente nunca depende solo de la señal liviana para saber el estado real.

## Salas, códigos y QR

- Código de sala de 6 caracteres, alfabeto `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` (sin
  `0 O 1 I`, para evitar ambigüedad al transcribirlo a mano).
- El QR codifica la URL completa de deep link (`https://host/join/ABC123`), generado en
  el servidor con `QRCoder`.
- **Capacidad 2 forzada atómicamente**: si dos personas escanean el QR a la vez,
  exactamente una entra y la otra recibe `RoomFull`. Requiere lock por sala o
  compare-and-swap sobre `IGameStore` — no una comprobación-luego-escritura sin
  atomicidad. Todo cambio aquí necesita un test concurrente que lo demuestre.
- TTL de sala: 30 min sin actividad → `Abandoned`, código liberado por un job de limpieza
  en background.

## Desconexión y reconexión

- Desconexión → `Paused`, ventana de gracia de 120s contada **en el servidor** (nunca en
  el cliente). El oponente ve `OpponentDisconnected(secondsRemaining)`. Al expirar:
  `Finished` por `Forfeit`.
- Al reconectar: resync completo — estado proyectado, eliminaciones propias e historial
  de preguntas de la partida — vía `StateSync`, no eventos incrementales.
- El cliente debe implementar reconexión automática de SignalR con backoff, y disparar
  `RequestResync` al detectar un salto de `StateVersion`.

## Errores

Toda acción rechazada por el motor o por el Hub (autorización, actor equivocado, fase
equivocada, sala llena, etc.) se comunica como `Error(code, message)` tipado — nunca como
una excepción que llegue al cliente. Los códigos de error son parte del contrato en
`Contracts` (enum o constantes), no strings libres.

## Implementación (Fase 3)

- `CreateRoom`/`JoinRoom` **sí llevan `playerId`** — el spec original los listaba sin él,
  pero sin ese parámetro el servidor no tiene forma de asociar la conexión a una
  identidad (el `PlayerId` lo genera el cliente y persiste en `localStorage`, no lo
  asigna el servidor). El resto de acciones lo resuelven vía `ConnectionRegistry`
  (`ConnectionId` → código + `PlayerId`), poblado en `AttachAsync` al crear/unirse.
- Cada conexión se agrega a **dos** grupos de SignalR: `room:{code}` (broadcasts sin
  datos sensibles: `RoomUpdated`, `QuestionAsked`, `TurnEnded`, `GameOver`,
  `OpponentDisconnected`, `OpponentReconnected`) y `room:{code}:player:{playerId}`
  (`StateSync`/`GameStarted`, siempre con la proyección **ya redactada** para ese
  jugador específico — nunca se manda un `Projection` sin filtrar al grupo de sala
  completo).
- **`GameStarted` se dispara en la transición `Lobby → InTurn`** (cuando el segundo
  `SetReady` completa), no cuando la sala se llena a 2 jugadores — eso solo dispara
  `StateSync` (la partida sigue en `Lobby`, esperando el otro Ready).
- **Gotcha de SignalR:** un evento sin payload debe mandarse con `SendAsync(evento)`
  **sin** un segundo argumento — mandar `SendAsync(evento, null)` sí entrega el mensaje,
  pero un handler cliente de aridad cero (`connection.On(nombre, () => ...)`) nunca lo
  recibe porque la aridad no calza. `GameEventPublisher.PushToRoomAsync` ya hace esta
  distinción; si se agrega un evento nuevo sin payload, no pasarle `null` a mano.
- Timeouts (pregunta 60s, desconexión 120s) los detecta `RoomActivityMonitor`
  (`BackgroundService`) llamando `Match.AdvanceTime()` en un barrido periódico — nunca
  hay un timer por partida. `RoomService.TickAsync`/`SweepOnceAsync` son testeables
  directamente contra un `IClock` falso, sin esperar tiempo real.
- `RoomService` mantiene una caché en memoria de `Match` vivos por código, con un lock
  por sala (no un lock global) para que la capacidad-2 sea atómica sin bloquear otras
  salas. Cada mutación se persiste de inmediato en `IGameStore` (write-through), y
  `Match.ToSnapshot()`/`FromSnapshot()` (agregado en Engine durante esta fase) es lo que
  permite reconstruir la partida si el proceso se reinicia con `SqliteGameStore`.
- El deck real de un content pack todavía no está conectado a `Match.Create` —
  `IDeckProvider`/`PlaceholderDeckProvider` entregan un mazo sintético de 16 cartas hasta
  que una fase posterior conecte el pack cargado (Fase 2) con el motor.
