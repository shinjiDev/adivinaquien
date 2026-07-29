using AdivinaQue.Engine;

namespace AdivinaQue.Server.Rooms;

/// <summary>
/// Envuelve el resultado de una operación sobre una sala. Los errores de sala
/// (<see cref="RoomActionError"/>, p. ej. sala llena) y los errores del motor
/// (<see cref="Engine.ErrorCode"/>, p. ej. actor equivocado) son capas distintas: el
/// Hub los traduce a <c>ErrorDto</c> con el <c>WireErrorCode</c> correspondiente.
/// </summary>
public sealed class RoomActionResult
{
    private RoomActionResult(
        bool isSuccess,
        RoomActionError? roomError,
        ErrorCode? engineError,
        RoomRecord? room,
        Match? match,
        bool isRejoin,
        bool wasReconnected,
        GameStatus? statusBefore = null)
    {
        IsSuccess = isSuccess;
        RoomError = roomError;
        EngineError = engineError;
        Room = room;
        Match = match;
        IsRejoin = isRejoin;
        WasReconnected = wasReconnected;
        StatusBefore = statusBefore;
    }

    public bool IsSuccess { get; }

    public RoomActionError? RoomError { get; }

    public ErrorCode? EngineError { get; }

    public RoomRecord? Room { get; }

    public Match? Match { get; }

    /// <summary>True si <see cref="RoomService.JoinRoomAsync"/> reconoció un PlayerId ya
    /// registrado en la sala (reconexión), no un jugador nuevo llenando un cupo.</summary>
    public bool IsRejoin { get; }

    /// <summary>True si este rejoin encontró la partida en <c>Paused</c> por este mismo
    /// jugador y disparó <c>Match.Reconnect</c> con éxito.</summary>
    public bool WasReconnected { get; }

    /// <summary>Estado del <see cref="Match"/> justo antes de aplicar la acción — permite
    /// detectar la transición Lobby→InTurn (para disparar GameStarted) sin una segunda
    /// consulta fuera del lock de la sala.</summary>
    public GameStatus? StatusBefore { get; }

    public static RoomActionResult Ok(RoomRecord room, Match? match, bool isRejoin = false, bool wasReconnected = false, GameStatus? statusBefore = null) =>
        new(true, null, null, room, match, isRejoin, wasReconnected, statusBefore);

    public static RoomActionResult Fail(RoomActionError error) => new(false, error, null, null, null, false, false);

    public static RoomActionResult FailEngine(ErrorCode engineError, RoomRecord room, Match match) =>
        new(false, null, engineError, room, match, false, false);
}
