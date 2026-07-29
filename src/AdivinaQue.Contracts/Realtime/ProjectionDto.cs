namespace AdivinaQue.Contracts.Realtime;

public sealed record ProjectionDto(
    GameStatusDto Status,
    TurnPhaseDto? Phase,
    Guid? ActivePlayerId,
    long StateVersion,
    IReadOnlyList<CardDto> Deck,
    CardDto? YourCard,
    IReadOnlyList<string> YourEliminations,
    IReadOnlyList<QuestionEntryDto> History,
    PauseInfoDto? Pause,
    FinishInfoDto? Finish);
