using System.Collections.Concurrent;
using AdivinaQue.Server.Rooms;

namespace AdivinaQue.Server.Persistence;

public sealed class InMemoryGameStore : IGameStore
{
    private readonly ConcurrentDictionary<string, string> _rooms = new();

    public Task<RoomRecord?> GetAsync(string code, CancellationToken ct = default)
    {
        var record = _rooms.TryGetValue(code, out var json) ? RoomRecordSerializer.Deserialize(json) : null;
        return Task.FromResult(record);
    }

    public Task SaveAsync(RoomRecord room, CancellationToken ct = default)
    {
        _rooms[room.Code] = RoomRecordSerializer.Serialize(room);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string code, CancellationToken ct = default)
    {
        _rooms.TryRemove(code, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RoomRecord>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<RoomRecord> all = _rooms.Values.Select(RoomRecordSerializer.Deserialize).ToList();
        return Task.FromResult(all);
    }
}
