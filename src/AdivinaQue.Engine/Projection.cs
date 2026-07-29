namespace AdivinaQue.Engine;

public sealed record QuestionView(
    Guid ActionId,
    Guid AskedByPlayerId,
    string Text,
    SuggestedFrom? SuggestedFrom,
    QuestionResolution? Resolution);

public sealed record PauseInfo(Guid DisconnectedPlayerId, DateTimeOffset PausedAt);

public sealed record FinishInfo(Guid Winner, FinishReason Reason, IReadOnlyDictionary<Guid, Card> RevealedCards);

public sealed record Projection(
    GameStatus Status,
    TurnPhase? Phase,
    Guid? ActivePlayerId,
    long StateVersion,
    IReadOnlyList<Card> Deck,
    Card? YourCard,
    IReadOnlyCollection<string> YourEliminations,
    IReadOnlyList<QuestionView> History,
    PauseInfo? Pause,
    FinishInfo? Finish);
