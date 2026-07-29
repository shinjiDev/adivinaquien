# Prompt para Claude Code — Juego tipo "Adivina Quién" online

> Pega este documento completo como primer mensaje en Claude Code, dentro de un
> directorio vacío. Está escrito para ejecutarse por fases: Claude Code debe
> **detenerse al final de cada fase** y esperar tu aprobación.

---

## Contexto y objetivo

Quiero construir un juego web multijugador para **exactamente 2 jugadores**, por turnos,
basado en la mecánica de "Adivina Quién": cada jugador tiene una carta secreta y debe
deducir la del oponente haciendo preguntas de sí/no sobre atributos.

La diferencia clave con el juego original: **el contenido es intercambiable**. El motor
no sabe nada del tema. El primer mazo será de bailes típicos de Chile, pero debe poder
cambiarse por trajes típicos, lugares, o cualquier otra cosa, solo reemplazando un JSON.

## Stack (no negociable)

- **Servidor:** ASP.NET Core 8, SignalR, Minimal APIs
- **Cliente:** Blazor WebAssembly
- **Contratos compartidos:** un proyecto `Contracts` referenciado por servidor y cliente,
  para que los DTOs y nombres de eventos no puedan divergir
- **Tests:** xUnit + FluentAssertions
- **Persistencia:** abstracción `IGameStore` con dos implementaciones: `InMemoryGameStore`
  y `SqliteGameStore`. Seleccionable por configuración.
- **Contenedor:** Dockerfile multi-stage desde el día 1
- **QR:** paquete NuGet `QRCoder`, generación en el servidor

Aún no está decidido el hosting. Por eso: **todo configurable por variables de entorno,
sin dependencias de un proveedor específico, y el estado nunca solo en memoria de proceso
en producción.**

## Estructura de proyecto

```
AdivinaQue/
├── src/
│   ├── AdivinaQue.Contracts/   # DTOs, eventos, enums, códigos de error
│   ├── AdivinaQue.Engine/      # motor puro: sin red, sin I/O, sin DateTime.Now
│   ├── AdivinaQue.Server/      # ASP.NET Core + SignalR Hub + IGameStore
│   ├── AdivinaQue.Client/      # Blazor WASM
│   └── AdivinaQue.PackTool/    # CLI para validar mazos y emitir reporte
├── tests/
│   ├── AdivinaQue.Engine.Tests/
│   └── AdivinaQue.Server.Tests/
├── content/bailes-chile/pack.json
├── docker/
├── .claude/skills/
└── CLAUDE.md
```

---

## Restricciones de diseño obligatorias

**Autoridad del servidor.** La carta secreta de cada jugador vive **solo** en el servidor
y jamás se serializa hacia el cliente del oponente. El estado que se envía a cada cliente
es una proyección personalizada, no el estado completo. Escribe un test que falle si la
carta secreta aparece en el payload enviado al oponente.

**Identidad ≠ conexión.** Cada jugador tiene un `PlayerId` (GUID) persistido en
`localStorage` del navegador. El `ConnectionId` de SignalR es efímero y cambia en cada
reconexión; nunca se usa como identidad. La reconexión es "el mismo `PlayerId` se vuelve
a asociar a la sala" y dispara un resync completo.

**Versionado de estado.** Cada mutación incrementa `StateVersion` (long, monotónico).
Cada evento enviado al cliente lleva su `StateVersion`. Si el cliente detecta un salto,
llama a `RequestResync`. Además, cada acción del cliente lleva una `ActionId` (GUID) para
idempotencia: reenviar la misma acción tras una reconexión no debe aplicarla dos veces.

**Motor puro.** `AdivinaQue.Engine` no referencia ASP.NET, no usa `DateTime.Now` (inyecta
`IClock`) ni `Random` sin semilla (inyecta `ISeededRandom`). Debe ser 100% determinista y
testeable sin levantar servidor. Toda la lógica de reglas vive ahí; el Hub solo traduce
mensajes y aplica autorización.

## Máquina de estados

```
Lobby            → 0-2 jugadores; se puede unir por código
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

Cada acción se valida contra la tupla `(Estado, Fase, ActorId)`. Una acción inválida
devuelve un `Error(code, message)` tipado; nunca lanza excepción hacia el cliente ni
altera el estado.

## Reglas del juego

### Roles dentro de un turno

Cada jugador tiene una carta secreta asignada, y es el **oponente** quien debe adivinarla.
Dentro de un mismo turno hay dos roles, que se invierten en el turno siguiente:

- **PREGUNTADOR** — el jugador de turno. Hace la pregunta e intenta deducir la carta del otro.
- **RESPONDEDOR** — el jugador pasivo. La pregunta es *sobre su carta secreta*, y él es el
  único humano que ve esa carta. Por eso es el único que puede equivocarse o mentir.

El servidor conoce ambas cartas, pero **no arbitra las respuestas**: solo las transporta
y las registra. Guarda las cartas secretas para asignarlas, proyectar el estado sin
filtrarlas y revelarlas al final.

### Flujo de la pregunta

1. El PREGUNTADOR escribe **la pregunta que quiera**, en texto libre. Ej: "¿tiene el
   pelo largo?", "¿se baila con pañuelo?", "¿es de una fiesta religiosa?".
2. El RESPONDEDOR la recibe junto a su propia carta —con sus atributos visibles como
   etiquetas— y pulsa **Sí**, **No** o **No aplica**.
3. Esa respuesta es definitiva. **El servidor no la valida ni la corrige.** El
   RESPONDEDOR es la autoridad sobre su propia carta, igual que en el juego de mesa.

No hay catálogo cerrado, ni constructor de expresiones, ni `MismatchPolicy`. El servidor
no interpreta el texto de la pregunta: solo lo transporta, lo registra en el historial y
lo muestra a ambos.

### Sugerencias, no restricciones

Debajo del campo de texto, muestra **4–6 preguntas sugeridas** derivadas de los atributos
del pack, como chips pulsables. Al tocar una, se rellena el campo de texto —que sigue
siendo editable— con la pregunta ya escrita.

Esto resuelve dos problemas de UX sin quitarle libertad a nadie: escribir en un teléfono
es lento, y un jugador nuevo frente a un campo vacío no sabe por dónde empezar. Rota las
sugerencias en cada turno para que no se vuelvan un menú fijo.

Si la pregunta vino de un chip, guarda `suggestedFrom: (attributeId, valueId)` junto al
texto. Sirve para métricas y para el modo asistido; no cambia nada del flujo.

### "No aplica": el tercer botón

En texto libre aparecen preguntas que no se pueden responder con sí o no: "¿tiene el pelo
largo?" sobre un personaje calvo, "¿de qué color es el pañuelo?" sobre uno que no usa.
Sin una tercera opción, obligas al RESPONDEDOR a mentir.

**No aplica** publica la pregunta como no respondida y **devuelve el turno al PREGUNTADOR
sin consumirlo**: puede reformular y preguntar de nuevo. El RESPONDEDOR no puede usarla
para bloquear, porque el historial la registra y queda visible para ambos.

### Confianza y disputas

Sin validación, el juego confía en el RESPONDEDOR, igual que en la mesa. La resolución es
social, no técnica: **al revelar las cartas al final de la partida, muestra el historial
completo de preguntas y respuestas junto a la carta revelada.** Ahí ambos ven de
inmediato si hubo un error o una mentira. Eso es todo lo que se necesita.

Un hueco conocido y aceptado: el PREGUNTADOR puede escribir "¿es la Cueca?" como pregunta
en vez de usar la acción de adivinar, esquivando el riesgo de perder. No tiene solución
técnica en texto libre, y es exactamente el mismo hueco que tiene el juego físico. Ponlo
como una línea en las reglas dentro de la app y no construyas nada para detectarlo.

### Bloqueo y timeout

`AwaitingAnswer` es la única fase donde **el actor es el jugador pasivo**, así que
necesita su propio manejo:

- **Temporizador de 60s.** Al expirar, la pregunta se cancela y el turno vuelve al
  PREGUNTADOR **sin consumirse**. El servidor no puede responder por el RESPONDEDOR
  porque ya no calcula respuestas.
- **Desconexión del RESPONDEDOR** durante esta fase: la sala pasa a `Paused` con el
  mecanismo general de reconexión. Al volver, retoma la pregunta pendiente.

### Resto de las reglas

- Las **eliminaciones son libres**: cada jugador tacha cartas en su propio tablero, en
  cualquier momento, sin validación del servidor. Es su cuaderno de deducción, y puede
  equivocarse. El servidor las persiste solo para poder restaurarlas al reconectar.
- Una pregunta por turno. Tras la respuesta, el PREGUNTADOR puede tachar cartas y
  luego termina su turno explícitamente (`EndTurn`).
- **Adivinar** es una acción alternativa a preguntar y termina la partida de inmediato:
  acierto = victoria, error = derrota. Configurable a variante "pierdes el turno".
- Temporizador de turno opcional, contado en el servidor, por defecto desactivado.

## Salas y reconexión

- Código de sala de 6 caracteres, alfabeto sin ambigüedades: `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`
  (sin `0 O 1 I`).
- El QR codifica la URL completa de deep link (`https://host/join/ABC123`), no solo el
  código, para que la cámara del teléfono la abra directamente.
- **Capacidad 2 forzada atómicamente.** Si dos personas escanean el QR simultáneamente,
  exactamente una entra y la otra recibe `RoomFull` limpiamente. Usa un lock por sala o
  compare-and-swap; escribe un test concurrente que lo demuestre.
- TTL de sala: 30 min sin actividad → `Abandoned` y el código se libera. Job de limpieza
  en background.
- Desconexión → estado `Paused` con ventana de gracia de 120s. El oponente ve una cuenta
  regresiva. Al expirar: `Finished` por `Forfeit`. El temporizador corre en el servidor,
  nunca en el cliente.
- Al reconectar: resync completo (estado proyectado + eliminaciones propias + historial
  de preguntas de la partida).

## Contrato de eventos

Define todo en `Contracts` como constantes o un enum, nunca strings mágicos dispersos.

**Cliente → Servidor:** `CreateRoom`, `JoinRoom(code)`, `SetReady`,
`AskQuestion(actionId, text, suggestedFrom?)`, `SubmitAnswer(actionId, Yes|No|NotApplicable)`,
`ToggleElimination(cardId)`, `EndTurn(actionId)`, `MakeGuess(actionId, cardId)`,
`RequestResync`, `LeaveRoom`

`text` es libre, máximo 200 caracteres, sanitizado antes de reenviarlo al otro cliente.
`suggestedFrom` es opcional y solo se rellena si la pregunta salió de un chip sugerido.

**Servidor → Cliente:** `RoomUpdated`, `GameStarted`, `StateSync`, `QuestionAsked(text)`,
`AnswerGiven(Yes|No|NotApplicable)`, `QuestionExpired`, `TurnEnded`,
`OpponentDisconnected(secondsRemaining)`, `OpponentReconnected`,
`GameOver(winner, reason, revealedCards, questionLog)`, `Error(code, message)`

Todos los eventos servidor→cliente incluyen `StateVersion`.

---

## Fases de trabajo

Detente al final de cada fase, muestra un resumen de lo hecho y espera aprobación
antes de continuar.

### Fase 0 — Andamiaje
Solución .NET con los 5 proyectos y los 2 de test. `CLAUDE.md` con comandos de build,
test y run. `.editorconfig`. Dockerfile multi-stage. `dotnet build` y `dotnet test`
deben pasar en verde con un test trivial.

**Antes de empezar la Fase 1**, crea estas skills en `.claude/skills/`, cada una como
carpeta con su `SKILL.md` (frontmatter con `name` y `description`; la `description` debe
decir explícitamente cuándo activarse):

- `content-pack` — esquema y reglas de validación de mazos *(ya la tengo escrita, te la
  paso aparte; no la inventes)*
- `game-rules` — la máquina de estados, acciones legales por estado y reglas de victoria
- `realtime-contract` — el contrato de eventos completo, códigos de error y versionado
- `stack-conventions` — estructura de proyectos, DI, logging, estilo de tests

### Fase 1 — Motor puro + tests
`AdivinaQue.Engine` completo: máquina de estados, validación de acciones, resolución de
preguntas contra el pack, condiciones de victoria. Cero dependencias de red.

*Criterio de aceptación:* una partida completa se puede jugar de principio a fin en un
test unitario, con semilla fija y resultado reproducible. Cobertura de tests sobre las
transiciones de estado ≥90%, incluyendo cada acción ilegal en cada estado.

### Fase 2 — Content pack + validador
Modelo del pack, deserialización, y `AdivinaQue.PackTool` como CLI que valida un pack y
emite un reporte: distribución y entropía por pregunta, matriz de redundancia,
profundidad del árbol de decisión óptimo, colisiones de vectores.

Consulta la skill `content-pack` para el esquema y las 8 reglas de validación.

*Criterio de aceptación:* el CLI rechaza un pack con dos cartas de vector idéntico, y
rechaza uno donde alguna pregunta cae fuera del 15%–85%. Tests con packs sintéticos.

### Fase 3 — Servidor y tiempo real
`GameHub` de SignalR, gestión de salas, `IGameStore` con ambas implementaciones,
generación de QR, job de limpieza, proyección de estado por jugador.

*Criterio de aceptación:* test de integración con dos clientes SignalR reales que juegan
una partida completa; un test donde el cliente A se desconecta a media partida, reconecta
a los 30s y recupera el estado exacto; un test de dos joins concurrentes donde solo uno
entra; y un test donde el RESPONDEDOR no contesta en 60s y el turno vuelve al
PREGUNTADOR sin consumirse ni bloquear la partida.

### Fase 4 — Cliente Blazor
Pantallas: inicio (crear / unirse), sala de espera con QR y código, tablero de juego,
fin de partida con revelación. Mobile-first, el tablero de 24 cartas debe funcionar en
pantalla de teléfono. Indicadores claros de: de quién es el turno, en qué fase está,
historial de preguntas, y estado de conexión del oponente.

Reconexión automática de SignalR con backoff, y detección de salto de `StateVersion`
que dispare resync.

### Fase 5 — Mazo de bailes típicos de Chile
Genera un **borrador** de 24 cartas siguiendo la skill `content-pack`, con el campo
`fuente` lleno en cada carta. Corre el validador e itera hasta que pase todas las reglas.

Marca el archivo claramente como borrador pendiente de verificación humana, y entrégame
además una tabla en markdown con la matriz cartas × atributos para que yo la revise.
No inventes datos folclóricos con confianza: si no estás seguro de un atributo, márcalo
con `"_verificar": true` en lugar de adivinar.

Las imágenes las proveo yo; usa placeholders con el nombre del archivo esperado.

### Fase 6 — Despliegue
`docker-compose.yml` para correr local. README con instrucciones. Configuración por
variables de entorno. Documenta qué proveedores sirven y cuáles no: **los free tiers que
duermen el contenedor rompen WebSockets y matan partidas en curso.** Recomienda opciones
que mantengan el proceso vivo.

---

## Anti-objetivos (no construyas esto)

Sin cuentas de usuario ni login. Sin matchmaking ni salas públicas. Sin chat de texto.
Sin espectadores. Sin más de 2 jugadores. Sin microservicios, sin ORM pesado, sin
event sourcing. Sin librería de estado del lado cliente más allá de lo que trae Blazor.

## Cómo quiero que trabajes

Antes de escribir código en cada fase, muéstrame el plan y las decisiones de diseño que
vas a tomar. Si una instrucción de este documento choca con algo que descubres
implementando, dímelo en vez de resolverlo silenciosamente. Prefiero menos código y más
tests sobre las reglas.
