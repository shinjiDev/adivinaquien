namespace AdivinaQue.Contracts.Realtime;

/// <summary>
/// Códigos de error transportados en <see cref="ErrorDto"/>. Los primeros cinco
/// reflejan <c>AdivinaQue.Engine.ErrorCode</c> (motor); el resto son errores del propio
/// Hub/sala, que el motor no conoce.
/// </summary>
public enum WireErrorCode
{
    WrongActor,
    WrongState,
    WrongPhase,
    TextTooLong,
    UnknownCard,
    RoomNotFound,
    RoomFull,
    PlayerNotInRoom,
    InvalidRequest,
}
