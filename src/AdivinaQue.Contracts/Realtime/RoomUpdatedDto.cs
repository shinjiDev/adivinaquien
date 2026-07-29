namespace AdivinaQue.Contracts.Realtime;

public sealed record RoomUpdatedDto(string Code, IReadOnlyList<Guid> PlayerIds);
