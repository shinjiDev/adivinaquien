---
name: stack-conventions
description: Convenciones de estructura de proyectos, referencias entre ellos, configuración compartida (Directory.Build.props, NuGet.Config) y estilo de tests de la solución AdivinaQue. Actívala al crear un proyecto nuevo, agregar una referencia o paquete NuGet, decidir dónde vive código nuevo, o escribir tests.
---

# Convenciones del stack

## Estructura y referencias entre proyectos

```
src/AdivinaQue.Contracts/   → sin dependencias de otros proyectos del repo
src/AdivinaQue.Engine/      → sin dependencias de otros proyectos del repo (pure, ver game-rules)
src/AdivinaQue.Client/      → referencia Contracts
src/AdivinaQue.Server/      → referencia Contracts, Engine y Client (ver "modelo de hosting" abajo)
src/AdivinaQue.PackTool/    → referencia Contracts
tests/AdivinaQue.Engine.Tests/    → referencia Engine
tests/AdivinaQue.PackTool.Tests/  → referencia Contracts + PackTool
tests/AdivinaQue.Server.Tests/    → referencia Server
```

`Engine` nunca referencia `Server` ni ningún paquete de ASP.NET — si un test o una
feature necesita eso, la lógica está en el proyecto equivocado.

## Modelo de hosting: Server sirve al Client

`AdivinaQue.Server` tiene una `ProjectReference` a `AdivinaQue.Client` y el paquete
`Microsoft.AspNetCore.Components.WebAssembly.Server` ("ASP.NET Core hosted Blazor
WebAssembly"). En `Program.cs`: `UseBlazorFrameworkFiles()` + `UseStaticFiles()` +
`MapFallbackToFile("index.html")`. Al publicar el Server, el output de Client
(wwwroot + framework WASM) se incluye automáticamente — un solo proceso, un solo puerto,
un solo contenedor. Elegido así porque el hosting final no está decidido y los free
tiers que duermen el proceso rompen WebSockets (ver Fase 6 del spec): menos piezas
móviles es más fácil de mantener vivo.

`Program.cs` del Server declara `public partial class Program;` al final para que
`WebApplicationFactory<Program>` funcione desde `Server.Tests` — no lo borres si tocas
ese archivo.

## Configuración compartida

- `Directory.Build.props` en la raíz fija `TargetFramework=net8.0`, `Nullable=enable`,
  `ImplicitUsings=enable`, `LangVersion=latest` para los 7 proyectos. Un csproj
  individual **no debe** redeclarar estas propiedades; si un proyecto necesita algo
  distinto (otro TFM, nullable disable), hazlo explícito ahí mismo y con un comentario
  de por qué se aparta del default.
- `NuGet.Config` en la raíz limita `packageSources` a `nuget.org` únicamente
  (`<clear />` primero). Se agregó porque el `NuGet.Config` global de una máquina de
  desarrollo puede traer feeds corporativos de otro proyecto que no resuelven DNS y
  rompen el restore de paquetes nuevos (pasó con el template de Blazor WASM en el
  andamiaje inicial). No lo quites ni le agregues fuentes sin necesidad real — el punto
  es que el build de este repo no dependa de qué feeds tenga configurados la máquina.

## Estilo de tests

- xUnit + FluentAssertions (`x.Should().Be(y)`), nunca `Assert.Equal`.
- Cada proyecto de test referencia únicamente el proyecto de `src/` que ejercita — no
  cruces `Engine.Tests` con `Server`, ni viceversa.
- `Server.Tests` usa `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`)
  para pruebas de integración reales contra el host, no mocks del pipeline HTTP.
- Nombre de archivo y clase describen qué se prueba (`HealthEndpointTests`, no
  `UnitTest1`) — el template por defecto de `dotnet new xunit` se reemplaza siempre.

## DI (establecido en Fase 3)

- Todo singleton: `IClock`, `IDeckProvider`, `ConnectionRegistry`, `GameEventPublisher`,
  `MatchOptions`, `RoomOptions`, `IGameStore`, `RoomService`, `RoomActivityMonitor`. No
  hay estado por-request en el Server (SignalR + Minimal API sobre servicios sin scope),
  así que `Scoped`/`Transient` no se usan todavía.
- `MatchOptions`/`RoomOptions` (clases de `Engine`/`Server`, no `IOptions<T>`) se
  construyen a mano en `Program.cs` leyendo `IConfiguration` con `GetValue(key, default)`
  — sin `services.Configure<T>()` ni `IOptionsMonitor`, porque estos valores no cambian
  en caliente y una clase plana inyectable directamente es más simple de pasarle a
  `Match.Create`/`RoomService` sin destapar el wrapper `IOptions<T>` en cada sitio.
- `RoomActivityMonitor` se registra dos veces a propósito:
  `services.AddSingleton<RoomActivityMonitor>()` **y**
  `services.AddHostedService(sp => sp.GetRequiredService<RoomActivityMonitor>())` — así
  el mismo objeto es a la vez el `BackgroundService` real y algo que un test puede
  resolver del contenedor para llamar `SweepOnceAsync()` directamente.
- Selección de `IGameStore` (`InMemoryGameStore` vs `SqliteGameStore`) por
  `Storage:Provider` en configuración — ver `Program.cs`. `SqliteGameStore` es
  `IDisposable` (mantiene una única `SqliteConnection` abierta); el contenedor de DI la
  cierra sola al apagar el host porque está registrada como singleton.

## Pendiente de decidir (no inventar hasta que la fase correspondiente lo requiera)

- **Logging**: no hay proveedor ni structured-logging decidido todavía; usar
  `ILogger<T>` por defecto (ya se usa así en `RoomActivityMonitor`) hasta que se decida
  algo más específico.

Actualiza esta sección (y quítala de "pendiente") en cuanto esa decisión se tome, en vez
de dejar que quede desactualizada.
