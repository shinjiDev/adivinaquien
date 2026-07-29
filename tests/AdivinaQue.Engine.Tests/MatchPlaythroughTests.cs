using AdivinaQue.Engine.Tests.TestDoubles;
using FluentAssertions;

namespace AdivinaQue.Engine.Tests;

public class MatchPlaythroughTests
{
    [Fact]
    public void FullGame_FromReadyToCorrectGuess_ProducesReproducibleResult()
    {
        var firstRun = PlayFullGame();
        var secondRun = PlayFullGame();

        firstRun.Winner.Should().Be(secondRun.Winner);
        firstRun.Reason.Should().Be(secondRun.Reason);
        firstRun.RevealedCardIds.Should().Equal(secondRun.RevealedCardIds);
        firstRun.HistoryResolutions.Should().Equal(secondRun.HistoryResolutions);
    }

    [Fact]
    public void AskQuestion_FromSuggestedChip_RecordsSuggestedFromInHistory()
    {
        var match = MatchFactory.CreateInTurn(new FakeClock(DateTimeOffset.UtcNow));
        var suggestion = new SuggestedFrom("zona", "norte");

        match.AskQuestion(Guid.NewGuid(), match.ActivePlayerId, "¿Es de la zona norte?", suggestion)
            .IsSuccess.Should().BeTrue();

        var projection = match.GetProjection(match.ActivePlayerId);
        projection.History.Single().SuggestedFrom.Should().Be(suggestion);
    }

    [Fact]
    public void FullGame_EndsInFinishedWithCorrectGuessAndFullHistory()
    {
        var result = PlayFullGame();

        result.Winner.Should().NotBeNull();
        result.Reason.Should().Be(FinishReason.CorrectGuess);
        result.HistoryResolutions.Should().Equal(
            QuestionResolution.NotApplicable,
            QuestionResolution.Yes,
            QuestionResolution.No);
    }

    private static PlaythroughResult PlayFullGame()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = MatchFactory.CreateLobby(clock);

        match.SetReady(MatchFactory.PlayerA).IsSuccess.Should().BeTrue();
        match.SetReady(MatchFactory.PlayerB).IsSuccess.Should().BeTrue();
        match.Status.Should().Be(GameStatus.InTurn);

        var questioner = match.ActivePlayerId;
        var responder = MatchFactory.Responder(match);

        // Pregunta 1: el respondedor no puede contestar sí/no -> no consume el turno.
        match.AskQuestion(Guid.NewGuid(), questioner, "¿Se baila descalzo?").IsSuccess.Should().BeTrue();
        match.SubmitAnswer(Guid.NewGuid(), responder, Answer.NotApplicable).IsSuccess.Should().BeTrue();
        match.Phase.Should().Be(TurnPhase.AwaitingQuestion);
        match.ActivePlayerId.Should().Be(questioner);

        // Pregunta 2: responde Sí, pasa a eliminaciones y el preguntador tacha una carta.
        match.AskQuestion(Guid.NewGuid(), questioner, "¿Es de la zona norte?").IsSuccess.Should().BeTrue();
        match.SubmitAnswer(Guid.NewGuid(), responder, Answer.Yes).IsSuccess.Should().BeTrue();
        match.Phase.Should().Be(TurnPhase.AwaitingEliminations);

        var deck = MatchFactory.BuildDeck();
        var discardedCard = deck.First(c => c.Id != match.GetSecretCard(responder).Id).Id;
        match.ToggleElimination(questioner, discardedCard).IsSuccess.Should().BeTrue();
        match.EndTurn(Guid.NewGuid(), questioner).IsSuccess.Should().BeTrue();
        match.ActivePlayerId.Should().Be(responder);

        // Turno del segundo jugador: una pregunta más, respondida con No.
        var secondQuestioner = responder;
        var secondResponder = questioner;
        match.AskQuestion(Guid.NewGuid(), secondQuestioner, "¿Usa máscara?").IsSuccess.Should().BeTrue();
        match.SubmitAnswer(Guid.NewGuid(), secondResponder, Answer.No).IsSuccess.Should().BeTrue();
        match.EndTurn(Guid.NewGuid(), secondQuestioner).IsSuccess.Should().BeTrue();
        match.ActivePlayerId.Should().Be(questioner);

        // El PREGUNTADOR original adivina correctamente la carta del oponente y gana.
        var opponentCard = match.GetSecretCard(responder);
        match.MakeGuess(Guid.NewGuid(), questioner, opponentCard.Id).IsSuccess.Should().BeTrue();
        match.Status.Should().Be(GameStatus.Finished);

        var projection = match.GetProjection(questioner);
        projection.Finish.Should().NotBeNull();

        return new PlaythroughResult(
            match.Winner,
            match.Reason,
            projection.Finish!.RevealedCards.Values.Select(c => c.Id).OrderBy(id => id).ToList(),
            projection.History.Select(h => h.Resolution!.Value).ToList());
    }

    private sealed record PlaythroughResult(
        Guid? Winner,
        FinishReason? Reason,
        IReadOnlyList<string> RevealedCardIds,
        IReadOnlyList<QuestionResolution> HistoryResolutions);
}
