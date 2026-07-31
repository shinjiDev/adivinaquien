using AdivinaQue.Engine;
using AdivinaQue.Engine.Abstractions;
using AdivinaQue.Server.BackgroundServices;
using AdivinaQue.Server.Hubs;
using AdivinaQue.Server.Persistence;
using AdivinaQue.Server.Qr;
using AdivinaQue.Server.Rooms;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

// "../../content" es correcto para `dotnet run` (el directorio de trabajo es el del
// proyecto, src/AdivinaQue.Server/, y content/ vive dos niveles arriba, en la raíz del
// repo). El Dockerfile copia content/ como hermano del publish (WORKDIR /app) y
// sobreescribe esto a "content" por variable de entorno — ver docker-compose.yml.
var contentRoot = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration.GetValue("ContentPack:RootDirectory", "../../content")!));
var activePackId = builder.Configuration.GetValue("ContentPack:PackId", "characters")!;

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IDeckProvider>(_ => new ContentPackDeckProvider(contentRoot, activePackId));
builder.Services.AddSingleton<ConnectionRegistry>();
builder.Services.AddSingleton<GameEventPublisher>();

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new MatchOptions
    {
        AnswerTimeout = TimeSpan.FromSeconds(config.GetValue("Match:AnswerTimeoutSeconds", 60)),
        DisconnectGrace = TimeSpan.FromSeconds(config.GetValue("Match:DisconnectGraceSeconds", 120)),
        WrongGuessPolicy = config.GetValue("Match:WrongGuessPolicy", WrongGuessPolicy.EndsMatch),
    };
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new RoomOptions
    {
        Ttl = TimeSpan.FromMinutes(config.GetValue("Room:TtlMinutes", 30)),
        SweepInterval = TimeSpan.FromSeconds(config.GetValue("Room:SweepIntervalSeconds", 1)),
    };
});

builder.Services.AddSingleton<IGameStore>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var provider = config.GetValue("Storage:Provider", "InMemory");
    return string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase)
        ? new SqliteGameStore(config.GetValue<string>("Storage:SqliteConnectionString") ?? "Data Source=adivinaque.db")
        : new InMemoryGameStore();
});

builder.Services.AddSingleton<RoomService>();

builder.Services.AddSingleton<RoomActivityMonitor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RoomActivityMonitor>());

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Sirve todo content/ (imágenes de cada content pack) bajo /content — no solo el pack
// activo, para no tener que reiniciar el server al cambiar ContentPack:PackId.
if (Directory.Exists(contentRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(contentRoot),
        RequestPath = "/content",
    });
}

app.MapGet("/health", () => Results.Ok("healthy"));
app.MapHub<GameHub>("/hub/game");
app.MapQrEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
