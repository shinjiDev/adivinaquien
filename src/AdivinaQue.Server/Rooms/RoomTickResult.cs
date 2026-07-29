using AdivinaQue.Engine;

namespace AdivinaQue.Server.Rooms;

public enum RoomTickOutcome
{
    NotFound,
    Unchanged,
    Changed,
    Abandoned,
}

/// <summary>Resultado de un barrido (<see cref="RoomService.TickAsync"/>) sobre una sala.</summary>
public sealed record RoomTickResult(RoomTickOutcome Outcome, RoomRecord? Room, Match? Match)
{
    public static RoomTickResult NotFound() => new(RoomTickOutcome.NotFound, null, null);

    public static RoomTickResult Unchanged(RoomRecord room) => new(RoomTickOutcome.Unchanged, room, null);

    public static RoomTickResult Changed(RoomRecord room, Match match) => new(RoomTickOutcome.Changed, room, match);

    public static RoomTickResult Abandoned(RoomRecord room) => new(RoomTickOutcome.Abandoned, room, null);
}
