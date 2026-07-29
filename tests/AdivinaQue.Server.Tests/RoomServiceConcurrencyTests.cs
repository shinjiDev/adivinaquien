using AdivinaQue.Engine;
using AdivinaQue.Server.Persistence;
using AdivinaQue.Server.Rooms;
using AdivinaQue.Server.Tests.TestSupport;
using FluentAssertions;

namespace AdivinaQue.Server.Tests;

public class RoomServiceConcurrencyTests
{
    [Fact]
    public async Task ConcurrentJoins_ToSameCode_OnlyOneSucceeds()
    {
        // Criterio de aceptación explícito: si dos personas escanean el QR
        // simultáneamente, exactamente una entra y la otra recibe RoomFull.
        var store = new InMemoryGameStore();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var service = new RoomService(store, clock, new PlaceholderDeckProvider(), new MatchOptions(), new RoomOptions());

        var playerA = Guid.NewGuid();
        var code = await service.CreateRoomAsync(playerA);

        var playerB = Guid.NewGuid();
        var playerC = Guid.NewGuid();

        var results = await Task.WhenAll(
            service.JoinRoomAsync(code, playerB),
            service.JoinRoomAsync(code, playerC));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Count(r => !r.IsSuccess && r.RoomError == RoomActionError.RoomFull).Should().Be(1);
    }

    [Fact]
    public async Task JoinRoom_ByExistingPlayer_IsAlwaysARejoin_NotACapacityConflict()
    {
        var store = new InMemoryGameStore();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var service = new RoomService(store, clock, new PlaceholderDeckProvider(), new MatchOptions(), new RoomOptions());

        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var code = await service.CreateRoomAsync(playerA);
        await service.JoinRoomAsync(code, playerB);

        var rejoin = await service.JoinRoomAsync(code, playerA);

        rejoin.IsSuccess.Should().BeTrue();
        rejoin.IsRejoin.Should().BeTrue();
    }
}
