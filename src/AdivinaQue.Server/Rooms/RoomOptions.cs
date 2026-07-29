namespace AdivinaQue.Server.Rooms;

public sealed class RoomOptions
{
    public TimeSpan Ttl { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan SweepInterval { get; init; } = TimeSpan.FromSeconds(1);
}
