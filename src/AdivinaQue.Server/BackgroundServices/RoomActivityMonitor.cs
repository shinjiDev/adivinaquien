using AdivinaQue.Contracts.Realtime;
using AdivinaQue.Engine;
using AdivinaQue.Server.Hubs;
using AdivinaQue.Server.Rooms;

namespace AdivinaQue.Server.BackgroundServices;

/// <summary>
/// Detecta, sin que ningún cliente llame a nada, los dos casos donde el paso del
/// tiempo por sí solo cambia el estado de una partida: el timeout de 60s en
/// <c>AwaitingAnswer</c> y el forfeit tras 120s en <c>Paused</c>. También libera salas
/// inactivas por más de <see cref="RoomOptions.Ttl"/>. <see cref="SweepOnceAsync"/> es
/// pública y testeable directamente (con un <c>IClock</c> falso adelantado) para no
/// depender de esperas de tiempo real en los tests.
/// </summary>
public sealed class RoomActivityMonitor : BackgroundService
{
    private readonly RoomService _rooms;
    private readonly GameEventPublisher _events;
    private readonly RoomOptions _options;
    private readonly ILogger<RoomActivityMonitor> _logger;

    public RoomActivityMonitor(RoomService rooms, GameEventPublisher events, RoomOptions options, ILogger<RoomActivityMonitor> logger)
    {
        _rooms = rooms;
        _events = events;
        _options = options;
        _logger = logger;
    }

    public async Task SweepOnceAsync(CancellationToken ct = default)
    {
        var codes = await _rooms.GetAllRoomCodesAsync(ct);
        foreach (var code in codes)
        {
            var result = await _rooms.TickAsync(code, ct);
            if (result.Outcome != RoomTickOutcome.Changed)
            {
                continue;
            }

            var match = result.Match!;
            await _events.PushStateSyncAsync(code, match);

            await _events.PushToRoomAsync(
                code,
                match.Status == GameStatus.Finished ? EventNames.GameOver : EventNames.QuestionExpired,
                null);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error en el barrido de actividad de salas.");
            }

            await Task.Delay(_options.SweepInterval, stoppingToken);
        }
    }
}
