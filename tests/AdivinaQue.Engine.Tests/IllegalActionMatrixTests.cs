using AdivinaQue.Engine.Tests.TestDoubles;
using FluentAssertions;

namespace AdivinaQue.Engine.Tests;

public class IllegalActionMatrixTests
{
    private static FakeClock NewClock() => new(DateTimeOffset.UtcNow);

    // ---- SetReady: solo válido en Lobby ----

    [Fact]
    public void SetReady_UnknownPlayer_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateLobby(NewClock());

        match.SetReady(MatchFactory.Stranger).Error.Should().Be(ErrorCode.WrongActor);
    }

    [Theory]
    [MemberData(nameof(NonLobbyMatches))]
    public void SetReady_OutsideLobby_ReturnsWrongState(Func<FakeClock, Match> buildMatch)
    {
        var match = buildMatch(NewClock());

        match.SetReady(MatchFactory.PlayerA).Error.Should().Be(ErrorCode.WrongState);
    }

    // ---- ChooseCharacter: solo en Setup, carta debe existir en el mazo ----

    [Fact]
    public void ChooseCharacter_UnknownPlayer_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateSetup(NewClock());

        match.ChooseCharacter(MatchFactory.Stranger, "card-0").Error.Should().Be(ErrorCode.WrongActor);
    }

    [Fact]
    public void ChooseCharacter_WhileInLobby_ReturnsWrongState()
    {
        var match = MatchFactory.CreateLobby(NewClock());

        match.ChooseCharacter(MatchFactory.PlayerA, "card-0").Error.Should().Be(ErrorCode.WrongState);
    }

    [Fact]
    public void ChooseCharacter_AfterMatchAlreadyInTurn_ReturnsWrongState()
    {
        var match = MatchFactory.CreateInTurn(NewClock());

        match.ChooseCharacter(MatchFactory.PlayerA, "card-2").Error.Should().Be(ErrorCode.WrongState);
    }

    [Fact]
    public void ChooseCharacter_UnknownCard_ReturnsUnknownCard()
    {
        var match = MatchFactory.CreateSetup(NewClock());

        match.ChooseCharacter(MatchFactory.PlayerA, "no-existe").Error.Should().Be(ErrorCode.UnknownCard);
    }

    [Fact]
    public void ChooseCharacter_BothPlayersChoose_TransitionsToInTurnWithBothSecretCardsSet()
    {
        var match = MatchFactory.CreateSetup(NewClock());
        var deck = MatchFactory.BuildDeck();

        match.ChooseCharacter(MatchFactory.PlayerA, deck[0].Id).IsSuccess.Should().BeTrue();
        match.Status.Should().Be(GameStatus.Setup, "todavía falta que el segundo jugador elija");

        match.ChooseCharacter(MatchFactory.PlayerB, deck[1].Id).IsSuccess.Should().BeTrue();

        match.Status.Should().Be(GameStatus.InTurn);
        match.GetSecretCard(MatchFactory.PlayerA).Id.Should().Be(deck[0].Id);
        match.GetSecretCard(MatchFactory.PlayerB).Id.Should().Be(deck[1].Id);
    }

    [Fact]
    public void ChooseCharacter_CanBeChangedBeforeOpponentChooses()
    {
        var match = MatchFactory.CreateSetup(NewClock());
        var deck = MatchFactory.BuildDeck();

        match.ChooseCharacter(MatchFactory.PlayerA, deck[0].Id).IsSuccess.Should().BeTrue();
        match.ChooseCharacter(MatchFactory.PlayerA, deck[1].Id).IsSuccess.Should().BeTrue();

        match.Status.Should().Be(GameStatus.Setup);
        match.GetSecretCard(MatchFactory.PlayerA).Id.Should().Be(deck[1].Id);
    }

    // ---- AskQuestion: solo el PREGUNTADOR, solo en AwaitingQuestion ----

    [Fact]
    public void AskQuestion_UnknownPlayer_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateInTurn(NewClock());

        match.AskQuestion(Guid.NewGuid(), MatchFactory.Stranger, "¿Es del norte?").Error.Should().Be(ErrorCode.WrongActor);
    }

    [Fact]
    public void AskQuestion_WhileInLobby_ReturnsWrongState()
    {
        var match = MatchFactory.CreateLobby(NewClock());

        match.AskQuestion(Guid.NewGuid(), MatchFactory.PlayerA, "¿Es del norte?").Error.Should().Be(ErrorCode.WrongState);
    }

    [Fact]
    public void AskQuestion_DuringAwaitingAnswer_ReturnsWrongPhase()
    {
        var match = MatchFactory.CreateAwaitingAnswer(NewClock(), out _);

        match.AskQuestion(Guid.NewGuid(), match.ActivePlayerId, "¿Otra pregunta?").Error.Should().Be(ErrorCode.WrongPhase);
    }

    [Fact]
    public void AskQuestion_DuringAwaitingEliminations_ReturnsWrongPhase()
    {
        var match = MatchFactory.CreateAwaitingEliminations(NewClock());

        match.AskQuestion(Guid.NewGuid(), match.ActivePlayerId, "¿Otra pregunta?").Error.Should().Be(ErrorCode.WrongPhase);
    }

    [Fact]
    public void AskQuestion_ByResponder_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateInTurn(NewClock());
        var responder = MatchFactory.Responder(match);

        match.AskQuestion(Guid.NewGuid(), responder, "¿Puedo preguntar yo?").Error.Should().Be(ErrorCode.WrongActor);
    }

    [Fact]
    public void AskQuestion_TextTooLong_ReturnsTextTooLong()
    {
        var match = MatchFactory.CreateInTurn(NewClock());
        var text = new string('a', 201);

        match.AskQuestion(Guid.NewGuid(), match.ActivePlayerId, text).Error.Should().Be(ErrorCode.TextTooLong);
    }

    [Fact]
    public void AskQuestion_EmptyText_ReturnsTextTooLong()
    {
        var match = MatchFactory.CreateInTurn(NewClock());

        match.AskQuestion(Guid.NewGuid(), match.ActivePlayerId, "   ").Error.Should().Be(ErrorCode.TextTooLong);
    }

    // ---- SubmitAnswer: solo el RESPONDEDOR, solo en AwaitingAnswer ----

    [Fact]
    public void SubmitAnswer_UnknownPlayer_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateAwaitingAnswer(NewClock(), out _);

        match.SubmitAnswer(Guid.NewGuid(), MatchFactory.Stranger, Answer.Yes).Error.Should().Be(ErrorCode.WrongActor);
    }

    [Fact]
    public void SubmitAnswer_DuringAwaitingQuestion_ReturnsWrongPhase()
    {
        var match = MatchFactory.CreateInTurn(NewClock());
        var responder = MatchFactory.Responder(match);

        match.SubmitAnswer(Guid.NewGuid(), responder, Answer.Yes).Error.Should().Be(ErrorCode.WrongPhase);
    }

    [Fact]
    public void SubmitAnswer_DuringAwaitingEliminations_ReturnsWrongPhase()
    {
        var match = MatchFactory.CreateAwaitingEliminations(NewClock());
        var responder = MatchFactory.Responder(match);

        match.SubmitAnswer(Guid.NewGuid(), responder, Answer.Yes).Error.Should().Be(ErrorCode.WrongPhase);
    }

    [Fact]
    public void SubmitAnswer_ByQuestioner_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateAwaitingAnswer(NewClock(), out _);

        match.SubmitAnswer(Guid.NewGuid(), match.ActivePlayerId, Answer.Yes).Error.Should().Be(ErrorCode.WrongActor);
    }

    // ---- ToggleElimination: libre en InTurn/Paused, pero el cardId debe existir ----

    [Fact]
    public void ToggleElimination_UnknownPlayer_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateInTurn(NewClock());

        match.ToggleElimination(MatchFactory.Stranger, "card-0").Error.Should().Be(ErrorCode.WrongActor);
    }

    [Fact]
    public void ToggleElimination_WhileInLobby_ReturnsWrongState()
    {
        var match = MatchFactory.CreateLobby(NewClock());

        match.ToggleElimination(MatchFactory.PlayerA, "card-0").Error.Should().Be(ErrorCode.WrongState);
    }

    [Fact]
    public void ToggleElimination_AfterFinished_ReturnsWrongState()
    {
        var match = MatchFactory.CreateFinished(NewClock());

        match.ToggleElimination(MatchFactory.PlayerA, "card-0").Error.Should().Be(ErrorCode.WrongState);
    }

    [Fact]
    public void ToggleElimination_UnknownCard_ReturnsUnknownCard()
    {
        var match = MatchFactory.CreateInTurn(NewClock());

        match.ToggleElimination(MatchFactory.PlayerA, "no-existe").Error.Should().Be(ErrorCode.UnknownCard);
    }

    // ---- EndTurn: solo el PREGUNTADOR, solo en AwaitingEliminations ----

    [Fact]
    public void EndTurn_UnknownPlayer_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateAwaitingEliminations(NewClock());

        match.EndTurn(Guid.NewGuid(), MatchFactory.Stranger).Error.Should().Be(ErrorCode.WrongActor);
    }

    [Fact]
    public void EndTurn_DuringAwaitingQuestion_ReturnsWrongPhase()
    {
        var match = MatchFactory.CreateInTurn(NewClock());

        match.EndTurn(Guid.NewGuid(), match.ActivePlayerId).Error.Should().Be(ErrorCode.WrongPhase);
    }

    [Fact]
    public void EndTurn_DuringAwaitingAnswer_ReturnsWrongPhase()
    {
        var match = MatchFactory.CreateAwaitingAnswer(NewClock(), out _);

        match.EndTurn(Guid.NewGuid(), match.ActivePlayerId).Error.Should().Be(ErrorCode.WrongPhase);
    }

    [Fact]
    public void EndTurn_ByResponder_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateAwaitingEliminations(NewClock());
        var responder = MatchFactory.Responder(match);

        match.EndTurn(Guid.NewGuid(), responder).Error.Should().Be(ErrorCode.WrongActor);
    }

    // ---- MakeGuess: solo el PREGUNTADOR, solo en AwaitingQuestion ----

    [Fact]
    public void MakeGuess_UnknownPlayer_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateInTurn(NewClock());

        match.MakeGuess(Guid.NewGuid(), MatchFactory.Stranger, "card-0").Error.Should().Be(ErrorCode.WrongActor);
    }

    [Fact]
    public void MakeGuess_DuringAwaitingAnswer_ReturnsWrongPhase()
    {
        var match = MatchFactory.CreateAwaitingAnswer(NewClock(), out _);

        match.MakeGuess(Guid.NewGuid(), match.ActivePlayerId, "card-0").Error.Should().Be(ErrorCode.WrongPhase);
    }

    [Fact]
    public void MakeGuess_DuringAwaitingEliminations_ReturnsWrongPhase()
    {
        var match = MatchFactory.CreateAwaitingEliminations(NewClock());

        match.MakeGuess(Guid.NewGuid(), match.ActivePlayerId, "card-0").Error.Should().Be(ErrorCode.WrongPhase);
    }

    [Fact]
    public void MakeGuess_ByResponder_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateInTurn(NewClock());
        var responder = MatchFactory.Responder(match);

        match.MakeGuess(Guid.NewGuid(), responder, "card-0").Error.Should().Be(ErrorCode.WrongActor);
    }

    [Fact]
    public void MakeGuess_UnknownCard_ReturnsUnknownCard()
    {
        var match = MatchFactory.CreateInTurn(NewClock());

        match.MakeGuess(Guid.NewGuid(), match.ActivePlayerId, "no-existe").Error.Should().Be(ErrorCode.UnknownCard);
    }

    [Fact]
    public void MakeGuess_Wrong_WithDefaultPolicy_EndsMatch()
    {
        var clock = NewClock();
        var match = MatchFactory.CreateInTurn(clock);
        var responder = MatchFactory.Responder(match);
        var questioner = match.ActivePlayerId;
        var wrongCardId = MatchFactory.BuildDeck().First(c => c.Id != match.GetSecretCard(responder).Id).Id;

        match.MakeGuess(Guid.NewGuid(), questioner, wrongCardId).IsSuccess.Should().BeTrue();

        match.Status.Should().Be(GameStatus.Finished);
        match.Reason.Should().Be(FinishReason.WrongGuess);
        match.Winner.Should().Be(responder);
    }

    [Fact]
    public void MakeGuess_Wrong_WithLosesTurnPolicy_KeepsMatchGoing()
    {
        var clock = NewClock();
        var options = new MatchOptions { WrongGuessPolicy = WrongGuessPolicy.LosesTurn };
        var match = MatchFactory.CreateInTurn(clock, options: options);
        var responder = MatchFactory.Responder(match);
        var questioner = match.ActivePlayerId;
        var wrongCardId = MatchFactory.BuildDeck().First(c => c.Id != match.GetSecretCard(responder).Id).Id;

        match.MakeGuess(Guid.NewGuid(), questioner, wrongCardId).IsSuccess.Should().BeTrue();

        match.Status.Should().Be(GameStatus.InTurn);
        match.ActivePlayerId.Should().Be(responder);
        match.Phase.Should().Be(TurnPhase.AwaitingQuestion);
    }

    // ---- Disconnect / Reconnect ----

    [Fact]
    public void Disconnect_UnknownPlayer_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateInTurn(NewClock());

        match.Disconnect(MatchFactory.Stranger).Error.Should().Be(ErrorCode.WrongActor);
    }

    [Fact]
    public void Disconnect_WhileInLobby_ReturnsWrongState()
    {
        var match = MatchFactory.CreateLobby(NewClock());

        match.Disconnect(MatchFactory.PlayerA).Error.Should().Be(ErrorCode.WrongState);
    }

    [Fact]
    public void Reconnect_UnknownPlayer_ReturnsWrongActor()
    {
        var match = MatchFactory.CreatePaused(NewClock(), out _);

        match.Reconnect(MatchFactory.Stranger).Error.Should().Be(ErrorCode.WrongActor);
    }

    [Fact]
    public void Reconnect_WhileNotPaused_ReturnsWrongState()
    {
        var match = MatchFactory.CreateInTurn(NewClock());

        match.Reconnect(MatchFactory.PlayerA).Error.Should().Be(ErrorCode.WrongState);
    }

    [Fact]
    public void Reconnect_ByPlayerWhoNeverDisconnected_ReturnsWrongActor()
    {
        var match = MatchFactory.CreatePaused(NewClock(), out var pausedPlayerId);
        var otherPlayer = pausedPlayerId == MatchFactory.PlayerA ? MatchFactory.PlayerB : MatchFactory.PlayerA;

        match.Reconnect(otherPlayer).Error.Should().Be(ErrorCode.WrongActor);
    }

    // ---- Leave ----

    [Fact]
    public void Leave_UnknownPlayer_ReturnsWrongActor()
    {
        var match = MatchFactory.CreateInTurn(NewClock());

        match.Leave(MatchFactory.Stranger).Error.Should().Be(ErrorCode.WrongActor);
    }

    [Fact]
    public void Leave_AfterFinished_ReturnsWrongState()
    {
        var match = MatchFactory.CreateFinished(NewClock());

        match.Leave(MatchFactory.PlayerA).Error.Should().Be(ErrorCode.WrongState);
    }

    [Fact]
    public void Leave_AfterAbandoned_ReturnsWrongState()
    {
        var match = MatchFactory.CreateAbandoned(NewClock());

        match.Leave(MatchFactory.PlayerB).Error.Should().Be(ErrorCode.WrongState);
    }

    [Fact]
    public void Leave_WhileInLobby_TransitionsToAbandoned()
    {
        var match = MatchFactory.CreateLobby(NewClock());

        match.Leave(MatchFactory.PlayerA).IsSuccess.Should().BeTrue();

        match.Status.Should().Be(GameStatus.Abandoned);
    }

    [Fact]
    public void Leave_WhileInTurn_ForfeitsToOpponent()
    {
        var match = MatchFactory.CreateInTurn(NewClock());
        var leaver = match.ActivePlayerId;
        var opponent = MatchFactory.Responder(match);

        match.Leave(leaver).IsSuccess.Should().BeTrue();

        match.Status.Should().Be(GameStatus.Finished);
        match.Reason.Should().Be(FinishReason.Forfeit);
        match.Winner.Should().Be(opponent);
    }

    // ---- Idempotencia de ActionId ----

    [Fact]
    public void AskQuestion_ReplayedActionId_IsNoOpNotError()
    {
        var match = MatchFactory.CreateInTurn(NewClock());
        var actionId = Guid.NewGuid();

        match.AskQuestion(actionId, match.ActivePlayerId, "¿Primera vez?").IsSuccess.Should().BeTrue();
        var phaseAfterFirst = match.Phase;
        var versionAfterFirst = match.StateVersion;

        var replay = match.AskQuestion(actionId, match.ActivePlayerId, "¿Segunda vez?");

        replay.IsSuccess.Should().BeTrue();
        match.Phase.Should().Be(phaseAfterFirst);
        match.StateVersion.Should().Be(versionAfterFirst);
    }

    public static IEnumerable<object[]> NonLobbyMatches()
    {
        yield return new object[] { (Func<FakeClock, Match>)(clock => MatchFactory.CreateSetup(clock)) };
        yield return new object[] { (Func<FakeClock, Match>)(clock => MatchFactory.CreateInTurn(clock)) };
        yield return new object[] { (Func<FakeClock, Match>)(clock => MatchFactory.CreatePaused(clock, out _)) };
        yield return new object[] { (Func<FakeClock, Match>)(clock => MatchFactory.CreateFinished(clock)) };
        yield return new object[] { (Func<FakeClock, Match>)(clock => MatchFactory.CreateAbandoned(clock)) };
    }
}
