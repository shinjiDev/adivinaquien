namespace AdivinaQue.Engine;

/// <summary>
/// Estado completo de un <see cref="Match"/> como datos planos, para que
/// <c>AdivinaQue.Server</c> pueda persistirlo (p. ej. en <c>SqliteGameStore</c>) y
/// reconstruirlo tras un reinicio del proceso. No incluye <c>IClock</c>/<c>ISeededRandom</c>
/// — esos se vuelven a inyectar al restaurar, igual que en <see cref="Match.Create"/>.
/// </summary>
public sealed record MatchSnapshot(
    Guid PlayerA,
    Guid PlayerB,
    IReadOnlyList<Card> Deck,
    MatchOptions Options,
    GameStatus Status,
    TurnPhase Phase,
    Guid ActivePlayerId,
    long StateVersion,
    Guid? Winner,
    FinishReason? Reason,
    bool ReadyA,
    bool ReadyB,
    IReadOnlyDictionary<Guid, Card> SecretCards,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> Eliminations,
    IReadOnlyList<QuestionEntrySnapshot> History,
    Guid? PendingQuestionActionId,
    Guid? PausedPlayerId,
    DateTimeOffset? PausedAt,
    IReadOnlyList<Guid> ProcessedActionIds);

public sealed record QuestionEntrySnapshot(
    Guid ActionId,
    Guid AskedByPlayerId,
    string Text,
    SuggestedFrom? SuggestedFrom,
    DateTimeOffset AskedAt,
    QuestionResolution? Resolution,
    DateTimeOffset? ResolvedAt);
