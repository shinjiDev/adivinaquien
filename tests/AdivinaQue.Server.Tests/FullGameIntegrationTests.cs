using AdivinaQue.Contracts.Realtime;
using AdivinaQue.Server.Rooms;
using AdivinaQue.Server.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AdivinaQue.Server.Tests;

public class FullGameIntegrationTests : IAsyncLifetime
{
    private readonly ServerFixture _fixture = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _fixture.DisposeAsync().AsTask();

    [Fact]
    public async Task TwoRealClients_PlayFullGame_ToCorrectGuess()
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();

        await using var connA = _fixture.CreateHubConnection();
        await using var connB = _fixture.CreateHubConnection();

        var (code, a, b, activePlayerId) = await GameplaySetup.CreateReadyGameAsync(connA, connB, playerA, playerB);

        var (questioner, questionerCollector, responder, responderCollector) = activePlayerId == playerA
            ? (playerA, a, playerB, b)
            : (playerB, b, playerA, a);

        await questionerCollector.Connection.InvokeAsync("AskQuestion", Guid.NewGuid(), "¿Es de la zona norte?", null);
        var afterAsk = await HubEventCollector.WaitForAsync(responderCollector.StateSyncs.Reader, s => s.Phase == TurnPhaseDto.AwaitingAnswer);
        afterAsk.ActivePlayerId.Should().Be(questioner);

        await responderCollector.Connection.InvokeAsync("SubmitAnswer", Guid.NewGuid(), AnswerDto.Yes);
        var afterAnswer = await HubEventCollector.WaitForAsync(questionerCollector.StateSyncs.Reader, s => s.Phase == TurnPhaseDto.AwaitingEliminations);
        afterAnswer.History.Should().ContainSingle(h => h.Resolution == QuestionResolutionDto.Yes);

        await questionerCollector.Connection.InvokeAsync("EndTurn", Guid.NewGuid());
        var afterEndTurn = await HubEventCollector.WaitForAsync(responderCollector.StateSyncs.Reader, s => s.ActivePlayerId == responder);
        afterEndTurn.Phase.Should().Be(TurnPhaseDto.AwaitingQuestion);

        // El servidor de pruebas corre en el mismo proceso (WebApplicationFactory), así
        // que el test puede consultar RoomService directamente para saber qué carta
        // adivinar y forzar un desenlace determinista — acceso de caja blanca solo para
        // armar el escenario, ningún cliente real ve la carta secreta del oponente.
        var roomService = _fixture.Services.GetRequiredService<RoomService>();
        var match = await roomService.GetLiveMatchAsync(code);
        var opponentCard = match!.GetSecretCard(questioner);

        // Tras el EndTurn, quien respondía antes es ahora el jugador activo (le toca
        // preguntar o adivinar); su oponente es el PREGUNTADOR original.
        await responderCollector.Connection.InvokeAsync("MakeGuess", Guid.NewGuid(), opponentCard.Id);

        (await HubEventCollector.WaitAsync(responderCollector.GameOvers.Reader)).Should().BeTrue();
        var finalState = await HubEventCollector.WaitForAsync(responderCollector.StateSyncs.Reader, s => s.Status == GameStatusDto.Finished);

        finalState.Finish.Should().NotBeNull();
        finalState.Finish!.Winner.Should().Be(responder);
        finalState.Finish.Reason.Should().Be(FinishReasonDto.CorrectGuess);
        finalState.Finish.RevealedCards.Should().ContainKey(playerA).And.ContainKey(playerB);
    }
}
