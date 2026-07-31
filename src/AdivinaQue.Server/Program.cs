using AdivinaQue.Engine;
using AdivinaQue.Engine.Abstractions;
using AdivinaQue.Server.BackgroundServices;
using AdivinaQue.Server.Hubs;
using AdivinaQue.Server.Persistence;
using AdivinaQue.Server.Qr;
using AdivinaQue.Server.Rooms;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

// El default del host genérico (5s) puede no alcanzar para que GracefulShutdownService
// recorra todas las salas activas y avise a cada una antes de que el proceso termine.
// 25s deja margen bajo los terminationGracePeriodSeconds: 30 de la Container App
// (Fase 2) — si este timeout fuera igual o mayor, Container Apps podría matar el
// proceso a la fuerza (SIGKILL) antes de que el shutdown ordenado alcance a terminar.
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(25);
});

// Detrás del ingress de Container Apps la conexión TLS termina antes de llegar a este
// proceso: sin esto, HttpContext.Request.Scheme/Host ven el tráfico interno (http, host
// interno del contenedor) en vez de lo que el jugador realmente usó — y esas dos cosas
// son justo las que arma el QR (QrEndpoints.cs) para el deep link de invitación. Los
// "known networks/proxies" por defecto asumen una lista fija de proxies de confianza que
// no aplica a un ingress gestionado como el de Container Apps (la IP interna cambia),
// así que se limpian explícitamente en vez de dejar pasar el reenvío sin validar por
// accidente.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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

    if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        return new SqliteGameStore(config.GetValue<string>("Storage:SqliteConnectionString") ?? "Data Source=adivinaque.db");
    }

    if (string.Equals(provider, "Table", StringComparison.OrdinalIgnoreCase))
    {
        return new TableStorageGameStore(CreateTableServiceClient(config));
    }

    return new InMemoryGameStore();
});

// Sin esto, cada réplica nueva (cada cold start desde cero) genera sus propias claves
// de Data Protection efímeras — cualquier cookie/token cifrado con la clave de la
// réplica anterior deja de ser válido. Esta app no usa auth/cookies todavía, pero es
// el mismo storage account que Table Storage, así que persistirlas ahí de una vez es
// gratis en esfuerzo y evita sorpresas el día que se agregue algo que sí las necesite.
// Solo aplica cuando Storage:Provider=Table (producción): en InMemory/Sqlite local no
// hay múltiples réplicas que sincronizar, así que las claves efímeras por proceso no
// son un problema real ahí.
if (string.Equals(builder.Configuration.GetValue("Storage:Provider", "InMemory"), "Table", StringComparison.OrdinalIgnoreCase))
{
    var blobClient = CreateDataProtectionBlobClient(builder.Configuration);
    builder.Services.AddDataProtection()
        .PersistKeysToAzureBlobStorage(blobClient)
        .SetApplicationName("AdivinaQue");
}

builder.Services.AddSingleton<RoomService>();

builder.Services.AddSingleton<RoomActivityMonitor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RoomActivityMonitor>());

// Mismo patrón que RoomActivityMonitor: también registrado por su tipo concreto (no
// solo como IHostedService) para que los tests puedan llamar StopAsync directamente,
// sin depender de apagar el WebApplicationFactory completo para ejercitarlo.
builder.Services.AddSingleton<GracefulShutdownService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GracefulShutdownService>());

var app = builder.Build();

// Primero de toda la tubería: todo lo que viene después (QR, redirects, cookies si
// alguna vez las hay) debe ver ya el scheme/host reales, no los del ingress interno.
app.UseForwardedHeaders();

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

// A diferencia de /health (liveness trivial, siempre 200), /healthz confirma que el
// IGameStore configurado responde de verdad — es la que usan las sondas de startup/
// liveness/readiness de Container Apps (ver infra/modules/container-app.bicep, Fase 2).
// Con autoescalado a cero, una sonda que no chequea nada real dejaría pasar réplicas
// "listas" que en realidad no pueden hablar con el storage.
app.MapGet("/healthz", async (IGameStore store, CancellationToken ct) =>
{
    try
    {
        await store.PingAsync(ct);
        return Results.Ok("healthy");
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapHub<GameHub>("/hub/game");
app.MapQrEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

// Connection string (Storage:TableConnectionString) es para Azurite en local/tests.
// En producción no hay secretos (ver Fase 0 del plan de despliegue): Storage:TableEndpoint
// + una identidad administrada (Storage:ManagedIdentityClientId, o DefaultAzureCredential
// si no se especifica) autentican sin contraseñas ni claves de cuenta.
static TableServiceClient CreateTableServiceClient(IConfiguration config)
{
    var connectionString = config.GetValue<string>("Storage:TableConnectionString");
    if (!string.IsNullOrEmpty(connectionString))
    {
        return new TableServiceClient(connectionString);
    }

    var endpoint = config.GetValue<string>("Storage:TableEndpoint")
        ?? throw new InvalidOperationException(
            "Storage:TableEndpoint (o Storage:TableConnectionString para desarrollo local) es requerido cuando Storage:Provider=Table.");

    var clientId = config.GetValue<string>("Storage:ManagedIdentityClientId");
    TokenCredential credential = string.IsNullOrEmpty(clientId)
        ? new DefaultAzureCredential()
        : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));

    return new TableServiceClient(new Uri(endpoint), credential);
}

// Mismo esquema de configuración que CreateTableServiceClient (connection string para
// Azurite local, endpoint + managed identity en producción) — pero contra Blob Storage,
// que es el servicio que usa la persistencia de claves de Data Protection (Table
// Storage no tiene un adaptador equivalente).
static BlobClient CreateDataProtectionBlobClient(IConfiguration config)
{
    const string containerName = "dataprotection-keys";
    const string blobName = "keys.xml";

    BlobContainerClient containerClient;
    var connectionString = config.GetValue<string>("Storage:BlobConnectionString");
    if (!string.IsNullOrEmpty(connectionString))
    {
        containerClient = new BlobContainerClient(connectionString, containerName);
    }
    else
    {
        var endpoint = config.GetValue<string>("Storage:BlobEndpoint")
            ?? throw new InvalidOperationException(
                "Storage:BlobEndpoint (o Storage:BlobConnectionString para desarrollo local) es requerido cuando Storage:Provider=Table.");

        var clientId = config.GetValue<string>("Storage:ManagedIdentityClientId");
        TokenCredential credential = string.IsNullOrEmpty(clientId)
            ? new DefaultAzureCredential()
            : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));

        // BlobContainerClient(Uri, ...) espera la URI del CONTENEDOR, no la raíz de la
        // cuenta — Storage:BlobEndpoint es el endpoint de servicio
        // (https://cuenta.blob.core.windows.net/). Sin el segmento del contenedor, el
        // SDK arma una request malformada contra la API REST (visto en producción:
        // "InvalidQueryParameterValue" al hacer CreateIfNotExists).
        var containerUri = new Uri($"{endpoint.TrimEnd('/')}/{containerName}");
        containerClient = new BlobContainerClient(containerUri, credential);
    }

    containerClient.CreateIfNotExists();
    return containerClient.GetBlobClient(blobName);
}

public partial class Program;
