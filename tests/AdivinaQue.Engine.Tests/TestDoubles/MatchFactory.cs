using AdivinaQue.Engine.Abstractions;

namespace AdivinaQue.Engine.Tests.TestDoubles;

public static class MatchFactory
{
    public static readonly Guid PlayerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid PlayerB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");

    public static IReadOnlyList<Card> BuildDeck(int count = 6) =>
        Enumerable.Range(0, count).Select(i => new Card($"card-{i}")).ToList();

    public static Match CreateLobby(FakeClock clock, int seed = 42, MatchOptions? options = null) =>
        Match.Create(PlayerA, PlayerB, BuildDeck(), clock, new SeededRandom(seed), options);

    public static Match CreateInTurn(FakeClock clock, int seed = 42, MatchOptions? options = null)
    {
        var match = CreateLobby(clock, seed, options);
        match.SetReady(PlayerA);
        match.SetReady(PlayerB);
        return match;
    }

    public static Match CreateAwaitingAnswer(FakeClock clock, out Guid questionActionId, int seed = 42, MatchOptions? options = null)
    {
        var match = CreateInTurn(clock, seed, options);
        questionActionId = Guid.NewGuid();
        match.AskQuestion(questionActionId, match.ActivePlayerId, "¿Es de la zona norte?");
        return match;
    }

    public static Match CreateAwaitingEliminations(FakeClock clock, int seed = 42, MatchOptions? options = null)
    {
        var match = CreateAwaitingAnswer(clock, out _, seed, options);
        var responder = Responder(match);
        match.SubmitAnswer(Guid.NewGuid(), responder, Answer.Yes);
        return match;
    }

    public static Guid Responder(Match match) => match.ActivePlayerId == PlayerA ? PlayerB : PlayerA;

    public static Match CreatePaused(FakeClock clock, out Guid pausedPlayerId, int seed = 42, MatchOptions? options = null)
    {
        var match = CreateInTurn(clock, seed, options);
        pausedPlayerId = match.ActivePlayerId;
        match.Disconnect(pausedPlayerId);
        return match;
    }

    public static Match CreateFinished(FakeClock clock, int seed = 42, MatchOptions? options = null)
    {
        var match = CreateInTurn(clock, seed, options);
        var opponentCard = match.GetSecretCard(Responder(match));
        match.MakeGuess(Guid.NewGuid(), match.ActivePlayerId, opponentCard.Id);
        return match;
    }

    public static Match CreateAbandoned(FakeClock clock, int seed = 42, MatchOptions? options = null)
    {
        var match = CreateLobby(clock, seed, options);
        match.Leave(PlayerA);
        return match;
    }
}
