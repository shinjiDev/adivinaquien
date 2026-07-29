using AdivinaQue.Contracts.Realtime;
using AdivinaQue.Engine;
using AdivinaQue.Server.Mapping;
using Microsoft.AspNetCore.SignalR;

namespace AdivinaQue.Server.Hubs;

/// <summary>
/// Empuja eventos a los grupos de SignalR de una sala. Se usa desde <see cref="GameHub"/>
/// (en respuesta a una acción de un cliente) y desde <c>RoomActivityMonitor</c> (en
/// respuesta a un timeout detectado por el barrido de fondo) — ambos necesitan la misma
/// lógica de nombres de grupo y de redacción por jugador, así que vive una sola vez acá.
/// </summary>
public sealed class GameEventPublisher
{
    private readonly IHubContext<GameHub> _hub;

    public GameEventPublisher(IHubContext<GameHub> hub)
    {
        _hub = hub;
    }

    public static string RoomGroup(string code) => $"room:{code}";

    public static string PlayerGroup(string code, Guid playerId) => $"room:{code}:player:{playerId}";

    // Si payload es null hay que mandar el evento SIN argumentos (SendAsync(method)),
    // no con un argumento que vale null: el cliente registra handlers de aridad cero
    // para estos eventos (connection.On(name, () => ...)) y un argumento nulo de más
    // no calza con esa aridad — el mensaje se manda pero el handler nunca dispara.
    public Task PushToRoomAsync(string code, string eventName, object? payload) =>
        payload is null
            ? _hub.Clients.Group(RoomGroup(code)).SendAsync(eventName)
            : _hub.Clients.Group(RoomGroup(code)).SendAsync(eventName, payload);

    public async Task PushStateSyncAsync(string code, Match match, string eventName = EventNames.StateSync)
    {
        foreach (var playerId in new[] { match.PlayerA, match.PlayerB })
        {
            var dto = ProjectionMapper.ToDto(match.GetProjection(playerId));
            await _hub.Clients.Group(PlayerGroup(code, playerId)).SendAsync(eventName, dto);
        }
    }

    public Task PushErrorAsync(string connectionId, ErrorDto error) =>
        _hub.Clients.Client(connectionId).SendAsync(EventNames.Error, error);
}
