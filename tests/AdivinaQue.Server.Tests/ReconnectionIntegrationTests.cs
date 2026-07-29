using AdivinaQue.Contracts.Realtime;
using AdivinaQue.Server.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace AdivinaQue.Server.Tests;

public class ReconnectionIntegrationTests : IAsyncLifetime
{
    private readonly ServerFixture _fixture = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _fixture.DisposeAsync().AsTask();

    [Fact]
    public async Task PlayerDisconnectsMidGame_ReconnectsAfter30Seconds_RecoversExactState()
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();

        await using var connA = _fixture.CreateHubConnection();
        await using var connB = _fixture.CreateHubConnection();
        var (code, a, b, activePlayerId) = await GameplaySetup.CreateReadyGameAsync(connA, connB, playerA, playerB);

        // El jugador activo (le toca AwaitingQuestion) es quien se cae de la red.
        var disconnectingConn = activePlayerId == playerA ? connA : connB;
        var opponentCollector = activePlayerId == playerA ? b : a;

        await disconnectingConn.DisposeAsync();
        await HubEventCollector.WaitAsync(opponentCollector.OpponentDisconnected.Reader);

        // Adelanta el reloj falso 30s (bien dentro de la ventana de gracia de 120s) y
        // corre un barrido manual — nunca se espera tiempo real.
        _fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        await _fixture.GetMonitor().SweepOnceAsync();

        await using var reconnected = _fixture.CreateHubConnection();
        var reconnectedCollector = new HubEventCollector(reconnected);
        await reconnected.StartAsync();
        await reconnected.InvokeAsync("JoinRoom", code, activePlayerId);

        await HubEventCollector.WaitAsync(opponentCollector.OpponentReconnected.Reader);
        var recovered = await HubEventCollector.WaitForAsync(
            reconnectedCollector.StateSyncs.Reader,
            s => s.Status == GameStatusDto.InTurn);

        recovered.Phase.Should().Be(TurnPhaseDto.AwaitingQuestion);
        recovered.ActivePlayerId.Should().Be(activePlayerId);
        recovered.YourCard.Should().NotBeNull();
        recovered.Pause.Should().BeNull();
    }

    [Fact]
    public async Task PlayerDisconnectsMidGame_DoesNotReconnectWithin120Seconds_OpponentWinsByForfeit()
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();

        await using var connA = _fixture.CreateHubConnection();
        await using var connB = _fixture.CreateHubConnection();
        var (_, a, b, activePlayerId) = await GameplaySetup.CreateReadyGameAsync(connA, connB, playerA, playerB);

        var disconnectingConn = activePlayerId == playerA ? connA : connB;
        var opponentCollector = activePlayerId == playerA ? b : a;
        var opponentId = activePlayerId == playerA ? playerB : playerA;

        await disconnectingConn.DisposeAsync();
        await HubEventCollector.WaitAsync(opponentCollector.OpponentDisconnected.Reader);

        _fixture.Clock.Advance(TimeSpan.FromSeconds(120));
        await _fixture.GetMonitor().SweepOnceAsync();

        await HubEventCollector.WaitAsync(opponentCollector.GameOvers.Reader);
        var finalState = await HubEventCollector.WaitForAsync(opponentCollector.StateSyncs.Reader, s => s.Status == GameStatusDto.Finished);

        finalState.Finish.Should().NotBeNull();
        finalState.Finish!.Winner.Should().Be(opponentId);
        finalState.Finish.Reason.Should().Be(FinishReasonDto.Forfeit);
    }
}
