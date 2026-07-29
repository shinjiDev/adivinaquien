namespace AdivinaQue.Contracts.Realtime;

public sealed record PauseInfoDto(Guid DisconnectedPlayerId, DateTimeOffset PausedAt);
