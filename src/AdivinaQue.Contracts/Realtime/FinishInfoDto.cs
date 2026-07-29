namespace AdivinaQue.Contracts.Realtime;

public sealed record FinishInfoDto(Guid Winner, FinishReasonDto Reason, IReadOnlyDictionary<Guid, CardDto> RevealedCards);
