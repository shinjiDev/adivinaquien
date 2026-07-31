using AdivinaQue.Contracts.Realtime;
using AdivinaQue.Engine;
using AdivinaQue.Server.Rooms;

namespace AdivinaQue.Server.Mapping;

/// <summary>
/// Traduce entre los tipos de <c>AdivinaQue.Engine</c> (motor puro) y los DTO de
/// <c>AdivinaQue.Contracts.Realtime</c> (contrato de red). Engine no conoce Contracts;
/// este mapeo es responsabilidad del Server.
/// </summary>
public static class ProjectionMapper
{
    public static ProjectionDto ToDto(Projection projection) => new(
        ToDto(projection.Status),
        projection.Phase is null ? null : ToDto(projection.Phase.Value),
        projection.ActivePlayerId,
        projection.StateVersion,
        projection.Deck.Select(ToDto).ToList(),
        projection.YourCard is null ? null : ToDto(projection.YourCard),
        projection.YourEliminations.ToList(),
        projection.History.Select(ToDto).ToList(),
        projection.Pause is null ? null : ToDto(projection.Pause),
        projection.Finish is null ? null : ToDto(projection.Finish));

    public static CardDto ToDto(Card card) => new(card.Id, card.Nombre, card.Imagen);

    public static QuestionEntryDto ToDto(QuestionView question) => new(
        question.ActionId,
        question.AskedByPlayerId,
        question.Text,
        question.SuggestedFrom is null ? null : new SuggestedFromDto(question.SuggestedFrom.AttributeId, question.SuggestedFrom.ValueId),
        question.Resolution is null ? null : ToDto(question.Resolution.Value));

    public static PauseInfoDto ToDto(PauseInfo pause) => new(pause.DisconnectedPlayerId, pause.PausedAt);

    public static FinishInfoDto ToDto(FinishInfo finish) => new(
        finish.Winner,
        ToDto(finish.Reason),
        finish.RevealedCards.ToDictionary(kv => kv.Key, kv => ToDto(kv.Value)));

    public static GameStatusDto ToDto(GameStatus status) => status switch
    {
        GameStatus.Lobby => GameStatusDto.Lobby,
        GameStatus.Setup => GameStatusDto.Setup,
        GameStatus.InTurn => GameStatusDto.InTurn,
        GameStatus.Paused => GameStatusDto.Paused,
        GameStatus.Finished => GameStatusDto.Finished,
        GameStatus.Abandoned => GameStatusDto.Abandoned,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static TurnPhaseDto ToDto(TurnPhase phase) => phase switch
    {
        TurnPhase.AwaitingQuestion => TurnPhaseDto.AwaitingQuestion,
        TurnPhase.AwaitingAnswer => TurnPhaseDto.AwaitingAnswer,
        TurnPhase.AwaitingEliminations => TurnPhaseDto.AwaitingEliminations,
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    public static QuestionResolutionDto ToDto(QuestionResolution resolution) => resolution switch
    {
        QuestionResolution.Yes => QuestionResolutionDto.Yes,
        QuestionResolution.No => QuestionResolutionDto.No,
        QuestionResolution.NotApplicable => QuestionResolutionDto.NotApplicable,
        QuestionResolution.Expired => QuestionResolutionDto.Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(resolution)),
    };

    public static FinishReasonDto ToDto(FinishReason reason) => reason switch
    {
        FinishReason.CorrectGuess => FinishReasonDto.CorrectGuess,
        FinishReason.WrongGuess => FinishReasonDto.WrongGuess,
        FinishReason.Forfeit => FinishReasonDto.Forfeit,
        FinishReason.Timeout => FinishReasonDto.Timeout,
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static Answer ToEngine(AnswerDto answer) => answer switch
    {
        AnswerDto.Yes => Answer.Yes,
        AnswerDto.No => Answer.No,
        AnswerDto.NotApplicable => Answer.NotApplicable,
        _ => throw new ArgumentOutOfRangeException(nameof(answer)),
    };

    public static SuggestedFrom? ToEngine(SuggestedFromDto? dto) =>
        dto is null ? null : new SuggestedFrom(dto.AttributeId, dto.ValueId);

    public static WireErrorCode ToWireError(ErrorCode code) => code switch
    {
        ErrorCode.WrongActor => WireErrorCode.WrongActor,
        ErrorCode.WrongState => WireErrorCode.WrongState,
        ErrorCode.WrongPhase => WireErrorCode.WrongPhase,
        ErrorCode.TextTooLong => WireErrorCode.TextTooLong,
        ErrorCode.UnknownCard => WireErrorCode.UnknownCard,
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    public static WireErrorCode ToWireError(RoomActionError error) => error switch
    {
        RoomActionError.RoomNotFound => WireErrorCode.RoomNotFound,
        RoomActionError.RoomFull => WireErrorCode.RoomFull,
        RoomActionError.MatchNotStarted => WireErrorCode.InvalidRequest,
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };
}
