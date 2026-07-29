namespace AdivinaQue.Contracts.Realtime;

public sealed record QuestionEntryDto(
    Guid ActionId,
    Guid AskedByPlayerId,
    string Text,
    SuggestedFromDto? SuggestedFrom,
    QuestionResolutionDto? Resolution);
