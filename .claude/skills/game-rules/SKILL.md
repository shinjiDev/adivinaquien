---
name: game-rules
description: Reglas del juego "Adivina Quién" — máquina de estados, fases de turno, acciones legales por (Estado, Fase, ActorId), roles PREGUNTADOR/RESPONDEDOR y condiciones de victoria. Actívala al implementar o modificar AdivinaQue.Engine, al validar si una acción del cliente es legal, o al decidir qué transición de estado corresponde a una acción.
---

# Reglas del juego

Fuente completa: `docs/PROMPT-claude-code.md`. Este documento resume lo que
`AdivinaQue.Engine` debe implementar; no repite el contrato de red (ver skill
`realtime-contract`) ni el esquema de contenido (ver skill `content-pack`).

## Máquina de estados

```
Lobby            → 0-2 jugadores; se une por código
  ↓ (2 jugadores marcan Ready)
Setup            → el servidor asigna una carta secreta aleatoria a cada jugador
  ↓
InTurn           → { ActivePlayerId, Phase }
     Phase: AwaitingQuestion → AwaitingAnswer → AwaitingEliminations → TurnEnd
  ↓
Paused           → un jugador desconectado; cuenta regresiva del servidor
  ↓
Finished         → { Winner, Reason: CorrectGuess | WrongGuess | Forfeit | Timeout }
Abandoned        → sala muerta, se libera el código
```

Toda acción se valida contra la tupla `(Estado, Fase, ActorId)`. Una acción inválida
(actor equivocado, fase equivocada) devuelve un `Error(code, message)` tipado y **nunca
lanza excepción ni altera el estado**.

`AwaitingAnswer` es la única fase donde el actor válido es el jugador **pasivo**
(RESPONDEDOR), no el de turno — no lo trates como una excepción del modelo, es una fase
más con su propio actor esperado.

## Roles de turno

Los roles se invierten cada turno:

- **PREGUNTADOR** — jugador de turno. Escribe la pregunta e intenta deducir la carta del
  oponente.
- **RESPONDEDOR** — jugador pasivo. Ve su propia carta secreta y responde
  Sí / No / **No aplica**. Es la única autoridad sobre su carta: el motor **no valida ni
  corrige** la respuesta.

El motor conoce ambas cartas para asignarlas, proyectar el estado sin filtrarlas (ver
`realtime-contract`) y revelarlas al final. No arbitra el contenido de la pregunta ni de
la respuesta.

## Flujo de una pregunta

1. `AskQuestion`: el PREGUNTADOR envía texto libre (máx. 200 caracteres). El motor no lo
   interpreta, solo lo registra en el historial. Fase → `AwaitingAnswer`.
2. `SubmitAnswer`: el RESPONDEDOR contesta Yes / No / NotApplicable.
   - Yes/No: la respuesta es definitiva y queda en el historial. Fase →
     `AwaitingEliminations`.
   - NotApplicable: la pregunta queda registrada como no respondida y **el turno vuelve
     al PREGUNTADOR sin consumirse** (no cuenta como la pregunta del turno; puede
     reformular). Fase → `AwaitingQuestion`, mismo `ActivePlayerId`.
3. Timeout de 60s en `AwaitingAnswer`: si el RESPONDEDOR no contesta, la pregunta se
   cancela y el turno vuelve al PREGUNTADOR sin consumirse — mismo efecto que
   `NotApplicable`, disparado por el reloj del servidor (`IClock`), no por el cliente.

## Resto de acciones legales

- `ToggleElimination(cardId)`: libre, sin validación de contenido, disponible para
  cualquiera de los dos jugadores en cualquier momento de `InTurn`. El motor solo la
  persiste para poder restaurarla al reconectar.
- `EndTurn`: solo el PREGUNTADOR, solo en `AwaitingEliminations`. Invierte
  `ActivePlayerId` y vuelve a `AwaitingQuestion`.
- `MakeGuess(cardId)`: acción alternativa a preguntar, solo el PREGUNTADOR, en
  `AwaitingQuestion`. Termina la partida de inmediato:
  - acierto → `Finished(Winner: PREGUNTADOR, Reason: CorrectGuess)`
  - error → `Finished(Winner: RESPONDEDOR, Reason: WrongGuess)`
  - Configurable a una variante donde el error solo hace perder el turno en vez de la
    partida — mantenlo como un parámetro del motor, no como una rama de estado nueva.

## Determinismo

`AdivinaQue.Engine` no referencia ASP.NET, no usa `DateTime.Now` (inyecta `IClock`) ni
`Random` sin semilla (inyecta `ISeededRandom`). Una partida completa debe ser
reproducible en un test con semilla fija — esto es el criterio de aceptación de la
Fase 1, no una sugerencia de estilo.

## Huecos aceptados (no construir solución)

El PREGUNTADOR puede escribir el nombre exacto de la carta como "pregunta" en vez de
usar `MakeGuess`, evitando el riesgo de perder. Es el mismo hueco que tiene el juego
físico; no se detecta ni se bloquea a nivel de motor.

## Decisiones tomadas al implementar `Match` (Fase 1)

El spec no detallaba estos puntos; así quedaron resueltos en `src/AdivinaQue.Engine/Match.cs`
(y confirmados con el usuario cuando la ambigüedad lo requería):

- **`Setup` es instantáneo**, no un estado observable: `SetReady` que completa el segundo
  Ready asigna cartas y elige `ActivePlayerId` en la misma llamada, sin pasar por una
  fase intermedia esperable desde fuera. `GetProjection` nunca reporta `Status.Setup`.
- **Primer turno**: se elige al azar vía `ISeededRandom` (mismo mecanismo que la
  asignación de cartas), no según quién creó la sala.
- **`Leave` (mapea a `LeaveRoom` del contrato)**: en `Lobby`/`Setup` → `Abandoned` (no
  hubo partida). En `InTurn`/`Paused` → `Finished(Forfeit)` inmediato para el que se va,
  **sin** la ventana de gracia de 120s (esa es solo para caídas de red vía `Disconnect`).
- **`FinishReason.Timeout`** existe en el enum (por fidelidad al diagrama del spec) pero
  ningún flujo de Fase 1 lo produce — el único timeout implementado (60s en
  `AwaitingAnswer`) nunca termina la partida, solo expira la pregunta. Si una fase futura
  necesita que un timeout termine la partida, revisar si corresponde reusar este valor.
- **`MatchOptions.TurnTimeout`** existe como config pero está inerte (el spec lo marca
  opcional y desactivado por defecto, sin describir su efecto de expiración).
- **`ToggleElimination`** válida en `InTurn` y en `Paused` (es la libreta de deducción
  propia, no bloquea por desconexión del oponente), pero no en `Lobby`/`Setup`/`Finished`/
  `Abandoned`.
