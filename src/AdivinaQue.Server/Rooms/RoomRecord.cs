using AdivinaQue.Engine;

namespace AdivinaQue.Server.Rooms;

/// <summary>
/// Metadatos de una sala + el estado del <see cref="Match"/> (si ya se llenó y se
/// instanció), como datos planos para persistir en <c>IGameStore</c>.
/// </summary>
public sealed class RoomRecord
{
    public required string Code { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastActivityAt { get; set; }

    public Guid? PlayerA { get; set; }

    public Guid? PlayerB { get; set; }

    public MatchSnapshot? Match { get; set; }
}
