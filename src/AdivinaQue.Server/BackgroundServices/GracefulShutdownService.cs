using AdivinaQue.Contracts.Realtime;
using AdivinaQue.Server.Hubs;
using AdivinaQue.Server.Rooms;

namespace AdivinaQue.Server.BackgroundServices;

/// <summary>
/// Con autoescalado a cero (Container Apps) el proceso puede terminar en cualquier
/// momento en que no haya tráfico, no solo en un deploy — así que esto no es un caso
/// raro, es rutina. El estado de cada sala ya se persiste después de cada mutación
/// (write-through en <see cref="RoomService"/>), así que no hay nada nuevo que guardar
/// acá: lo único que falta es avisarle a cada cliente conectado, antes de que la
/// conexión se corte de golpe, que el corte es intencional — así <c>WithAutomaticReconnect</c>
/// del lado cliente lo trata como lo que es, no como un error.
/// </summary>
public sealed class GracefulShutdownService : IHostedService
{
    private readonly RoomService _rooms;
    private readonly GameEventPublisher _events;
    private readonly ILogger<GracefulShutdownService> _logger;

    public GracefulShutdownService(RoomService rooms, GameEventPublisher events, ILogger<GracefulShutdownService> logger)
    {
        _rooms = rooms;
        _events = events;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> codes;
        try
        {
            codes = await _rooms.GetAllRoomCodesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo obtener la lista de salas activas durante el apagado ordenado.");
            return;
        }

        foreach (var code in codes)
        {
            try
            {
                await _events.PushToRoomAsync(code, EventNames.ServerShuttingDown, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo avisar el apagado a la sala {Code}.", code);
            }
        }
    }
}
