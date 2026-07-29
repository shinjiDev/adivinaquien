using AdivinaQue.Server.Rooms;

namespace AdivinaQue.Server.Persistence;

public interface IGameStore
{
    Task<RoomRecord?> GetAsync(string code, CancellationToken ct = default);

    Task SaveAsync(RoomRecord room, CancellationToken ct = default);

    Task DeleteAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<RoomRecord>> GetAllAsync(CancellationToken ct = default);
}
