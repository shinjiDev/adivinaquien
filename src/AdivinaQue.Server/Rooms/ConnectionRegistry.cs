using System.Collections.Concurrent;

namespace AdivinaQue.Server.Rooms;

// Nombrado "PlayerConnection" (no "ConnectionInfo") porque ese nombre choca con
// Microsoft.AspNetCore.Http.ConnectionInfo, que ya está en scope por los usings
// implícitos del SDK Web.
public sealed record PlayerConnection(string RoomCode, Guid PlayerId);

/// <summary>
/// Asocia el <c>ConnectionId</c> efímero de SignalR a (código de sala, PlayerId).
/// Identidad ≠ conexión: esto es lo único que usa ConnectionId como clave, y solo para
/// enrutar mensajes de esta conexión concreta — nunca como identidad de jugador.
/// </summary>
public sealed class ConnectionRegistry
{
    private readonly ConcurrentDictionary<string, PlayerConnection> _connections = new();

    public void Register(string connectionId, string roomCode, Guid playerId) =>
        _connections[connectionId] = new PlayerConnection(roomCode, playerId);

    public bool TryGet(string connectionId, out PlayerConnection info)
    {
        var found = _connections.TryGetValue(connectionId, out var value);
        info = value!;
        return found;
    }

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);
}
