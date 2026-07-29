namespace AdivinaQue.Contracts.Realtime;

public sealed record ProjectionDto(
    GameStatusDto Status,
    TurnPhaseDto? Phase,
    Guid? ActivePlayerId,
    long StateVersion,
    CardDto? YourCard,
    IReadOnlyList<string> YourEliminations,
    IReadOnlyList<QuestionEntryDto> History,
    PauseInfoDto? Pause,
    FinishInfoDto? Finish);
