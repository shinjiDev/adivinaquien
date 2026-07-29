using AdivinaQue.Contracts.Realtime;
using AdivinaQue.Server.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace AdivinaQue.Server.Tests;

public class QuestionTimeoutIntegrationTests : IAsyncLifetime
{
    private readonly ServerFixture _fixture = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _fixture.DisposeAsync().AsTask();

    [Fact]
    public async Task ResponderDoesNotAnswerWithin60Seconds_TurnReturnsToQuestionerWithoutBeingConsumed()
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();

        await using var connA = _fixture.CreateHubConnection();
        await using var connB = _fixture.CreateHubConnection();
        var (_, a, b, activePlayerId) = await GameplaySetup.CreateReadyGameAsync(connA, connB, playerA, playerB);

        var (questioner, questionerCollector, _, responderCollector) = activePlayerId == playerA
            ? (playerA, a, playerB, b)
            : (playerB, b, playerA, a);

        var firstActionId = Guid.NewGuid();
        await questionerCollector.Connection.InvokeAsync("AskQuestion", firstActionId, "¿Es de la zona norte?", null);
        await HubEventCollector.WaitForAsync(responderCollector.StateSyncs.Reader, s => s.Phase == TurnPhaseDto.AwaitingAnswer);

        // El respondedor nunca contesta. El servidor lo detecta solo, sin que ningún
        // cliente llame a nada: adelantamos el reloj falso 60s y corremos un barrido.
        _fixture.Clock.Advance(TimeSpan.FromSeconds(60));
        await _fixture.GetMonitor().SweepOnceAsync();

        await HubEventCollector.WaitAsync(questionerCollector.QuestionExpired.Reader);
        var afterExpiry = await HubEventCollector.WaitForAsync(
            questionerCollector.StateSyncs.Reader,
            s => s.Phase == TurnPhaseDto.AwaitingQuestion);

        // El turno vuelve al PREGUNTADOR sin consumirse: sigue siendo su turno.
        afterExpiry.ActivePlayerId.Should().Be(questioner);
        afterExpiry.History.Should().ContainSingle(h => h.ActionId == firstActionId && h.Resolution == QuestionResolutionDto.Expired);

        // La partida no queda bloqueada: el mismo jugador puede seguir preguntando.
        var secondActionId = Guid.NewGuid();
        await questionerCollector.Connection.InvokeAsync("AskQuestion", secondActionId, "¿Usa máscara?", null);
        var afterSecondAsk = await HubEventCollector.WaitForAsync(
            responderCollector.StateSyncs.Reader,
            s => s.Phase == TurnPhaseDto.AwaitingAnswer && s.History.Any(h => h.ActionId == secondActionId));

        afterSecondAsk.History.Should().Contain(h => h.ActionId == secondActionId && h.Resolution == null);
    }
}
