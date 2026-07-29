using System.Collections.Concurrent;
using AdivinaQue.Engine;
using AdivinaQue.Engine.Abstractions;
using AdivinaQue.Server.Persistence;

namespace AdivinaQue.Server.Rooms;

/// <summary>
/// Orquesta salas: capacidad 2 forzada atómicamente (un lock por código), caché en
/// memoria de <see cref="Match"/> vivos, y persistencia write-through hacia
/// <see cref="IGameStore"/> después de cada mutación (para que <c>SqliteGameStore</c>
/// sobreviva un reinicio del proceso). La atomicidad de <see cref="JoinRoomAsync"/> no
/// depende de qué <see cref="IGameStore"/> esté seleccionado, porque el lock vive acá,
/// no en el store.
/// </summary>
public sealed class RoomService
{
    private readonly IGameStore _store;
    private readonly IClock _clock;
    private readonly IDeckProvider _deckProvider;
    private readonly MatchOptions _matchOptions;
    private readonly RoomOptions _roomOptions;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<string, Match> _liveMatches = new();

    public RoomService(IGameStore store, IClock clock, IDeckProvider deckProvider, MatchOptions matchOptions, RoomOptions roomOptions)
    {
        _store = store;
        _clock = clock;
        _deckProvider = deckProvider;
        _matchOptions = matchOptions;
        _roomOptions = roomOptions;
    }

    public async Task<string> CreateRoomAsync(Guid playerId, CancellationToken ct = default)
    {
        string code;
        RoomRecord? existing;
        do
        {
            code = RoomCode.Generate();
            existing = await _store.GetAsync(code, ct);
        }
        while (existing is not null);

        var room = new RoomRecord
        {
            Code = code,
            CreatedAt = _clock.UtcNow,
            LastActivityAt = _clock.UtcNow,
            PlayerA = playerId,
        };

        await _store.SaveAsync(room, ct);
        return code;
    }

    public async Task<RoomActionResult> JoinRoomAsync(string code, Guid playerId, CancellationToken ct = default)
    {
        var roomLock = GetLock(code);
        await roomLock.WaitAsync(ct);
        try
        {
            var room = await _store.GetAsync(code, ct);
            if (room is null)
            {
                return RoomActionResult.Fail(RoomActionError.RoomNotFound);
            }

            if (room.PlayerA == playerId || room.PlayerB == playerId)
            {
                var existingMatch = GetOrRehydrateMatch(room);
                var wasReconnected = false;
                if (existingMatch is not null && existingMatch.Status == GameStatus.Paused)
                {
                    wasReconnected = existingMatch.Reconnect(playerId).IsSuccess;
                    room.Match = existingMatch.ToSnapshot();
                }

                room.LastActivityAt = _clock.UtcNow;
                await _store.SaveAsync(room, ct);
                return RoomActionResult.Ok(room, existingMatch, isRejoin: true, wasReconnected: wasReconnected);
            }

            if (room.PlayerA is not null && room.PlayerB is not null)
            {
                return RoomActionResult.Fail(RoomActionError.RoomFull);
            }

            if (room.PlayerA is null)
            {
                room.PlayerA = playerId;
            }
            else
            {
                room.PlayerB = playerId;
            }

            Match? match = null;
            if (room.PlayerA is not null && room.PlayerB is not null)
            {
                match = Match.Create(
                    room.PlayerA.Value,
                    room.PlayerB.Value,
                    _deckProvider.GetDeck(),
                    _clock,
                    new SeededRandom(Random.Shared.Next()),
                    _matchOptions);
                _liveMatches[code] = match;
                room.Match = match.ToSnapshot();
            }

            room.LastActivityAt = _clock.UtcNow;
            await _store.SaveAsync(room, ct);
            return RoomActionResult.Ok(room, match);
        }
        finally
        {
            roomLock.Release();
        }
    }

    public async Task<RoomActionResult> ExecuteActionAsync(string code, Func<Match, Result> action, CancellationToken ct = default)
    {
        var roomLock = GetLock(code);
        await roomLock.WaitAsync(ct);
        try
        {
            var room = await _store.GetAsync(code, ct);
            if (room is null)
            {
                return RoomActionResult.Fail(RoomActionError.RoomNotFound);
            }

            var match = GetOrRehydrateMatch(room);
            if (match is null)
            {
                return RoomActionResult.Fail(RoomActionError.MatchNotStarted);
            }

            var statusBefore = match.Status;
            var result = action(match);

            room.Match = match.ToSnapshot();
            room.LastActivityAt = _clock.UtcNow;
            await _store.SaveAsync(room, ct);

            return result.IsSuccess
                ? RoomActionResult.Ok(room, match, statusBefore: statusBefore)
                : RoomActionResult.FailEngine(result.Error!.Value, room, match);
        }
        finally
        {
            roomLock.Release();
        }
    }

    public async Task<RoomActionResult> LeaveAsync(string code, Guid playerId, CancellationToken ct = default)
    {
        var roomLock = GetLock(code);
        await roomLock.WaitAsync(ct);
        try
        {
            var room = await _store.GetAsync(code, ct);
            if (room is null)
            {
                return RoomActionResult.Fail(RoomActionError.RoomNotFound);
            }

            if (room.Match is null)
            {
                await _store.DeleteAsync(code, ct);
                _liveMatches.TryRemove(code, out _);
                return RoomActionResult.Ok(room, null);
            }

            var match = GetOrRehydrateMatch(room)!;
            var result = match.Leave(playerId);
            if (!result.IsSuccess)
            {
                return RoomActionResult.FailEngine(result.Error!.Value, room, match);
            }

            if (match.Status == GameStatus.Abandoned)
            {
                await _store.DeleteAsync(code, ct);
                _liveMatches.TryRemove(code, out _);
            }
            else
            {
                room.Match = match.ToSnapshot();
                room.LastActivityAt = _clock.UtcNow;
                await _store.SaveAsync(room, ct);
            }

            return RoomActionResult.Ok(room, match);
        }
        finally
        {
            roomLock.Release();
        }
    }

    public async Task<Match?> GetLiveMatchAsync(string code, CancellationToken ct = default)
    {
        var roomLock = GetLock(code);
        await roomLock.WaitAsync(ct);
        try
        {
            var room = await _store.GetAsync(code, ct);
            return room is null ? null : GetOrRehydrateMatch(room);
        }
        finally
        {
            roomLock.Release();
        }
    }

    /// <summary>
    /// Barrido de una sala: aplica <see cref="Match.AdvanceTime"/> (timeouts de
    /// pregunta/desconexión) y el TTL de inactividad de la sala. Lo llama
    /// <c>RoomActivityMonitor</c> periódicamente, y los tests lo llaman directamente
    /// tras adelantar un reloj falso — nunca espera tiempo real.
    /// </summary>
    public async Task<RoomTickResult> TickAsync(string code, CancellationToken ct = default)
    {
        var roomLock = GetLock(code);
        await roomLock.WaitAsync(ct);
        try
        {
            var room = await _store.GetAsync(code, ct);
            if (room is null)
            {
                return RoomTickResult.NotFound();
            }

            if (room.Match is null)
            {
                return await ExpireIfIdleAsync(room, ct);
            }

            var match = GetOrRehydrateMatch(room)!;
            var versionBefore = match.StateVersion;
            match.AdvanceTime();

            if (match.StateVersion != versionBefore)
            {
                room.Match = match.ToSnapshot();
                room.LastActivityAt = _clock.UtcNow;
                await _store.SaveAsync(room, ct);
                return RoomTickResult.Changed(room, match);
            }

            return await ExpireIfIdleAsync(room, ct);
        }
        finally
        {
            roomLock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetAllRoomCodesAsync(CancellationToken ct = default)
    {
        var rooms = await _store.GetAllAsync(ct);
        return rooms.Select(r => r.Code).ToList();
    }

    private async Task<RoomTickResult> ExpireIfIdleAsync(RoomRecord room, CancellationToken ct)
    {
        if (_clock.UtcNow - room.LastActivityAt < _roomOptions.Ttl)
        {
            return RoomTickResult.Unchanged(room);
        }

        await _store.DeleteAsync(room.Code, ct);
        _liveMatches.TryRemove(room.Code, out _);
        return RoomTickResult.Abandoned(room);
    }

    private Match? GetOrRehydrateMatch(RoomRecord room)
    {
        if (_liveMatches.TryGetValue(room.Code, out var cached))
        {
            return cached;
        }

        if (room.Match is null)
        {
            return null;
        }

        var match = Match.FromSnapshot(room.Match, _clock, new SeededRandom(Random.Shared.Next()));
        _liveMatches[room.Code] = match;
        return match;
    }

    private SemaphoreSlim GetLock(string code) => _locks.GetOrAdd(code, _ => new SemaphoreSlim(1, 1));
}
