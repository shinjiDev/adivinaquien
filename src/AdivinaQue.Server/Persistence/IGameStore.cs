using AdivinaQue.Server.Rooms;

namespace AdivinaQue.Server.Persistence;

public interface IGameStore
{
    Task<RoomRecord?> GetAsync(string code, CancellationToken ct = default);

    Task SaveAsync(RoomRecord room, CancellationToken ct = default);

    Task DeleteAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<RoomRecord>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Confirma que el store puede responder de verdad (conexión/autenticación viva),
    /// sin traer ni tocar datos de ninguna sala. La usa /healthz — un endpoint que
    /// siempre devuelve 200 sin chequear nada no sirve de mucho detrás de un
    /// autoescalado a cero: si el store real (Table Storage en producción) no
    /// responde, Container Apps debe verlo en la sonda y no marcar la réplica lista.
    /// </summary>
    Task PingAsync(CancellationToken ct = default);
}
