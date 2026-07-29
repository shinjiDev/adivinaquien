namespace AdivinaQue.Contracts.Realtime;

/// <summary>
/// Nombres de los eventos servidor→cliente enviados por el <c>GameHub</c> vía
/// <c>SendAsync</c>/escuchados por el cliente vía <c>connection.On(...)</c>. Nunca
/// strings mágicos dispersos: todo referencia esta clase.
/// </summary>
public static class EventNames
{
    public const string RoomUpdated = "RoomUpdated";
    public const string GameStarted = "GameStarted";
    public const string StateSync = "StateSync";
    public const string QuestionAsked = "QuestionAsked";
    public const string AnswerGiven = "AnswerGiven";
    public const string QuestionExpired = "QuestionExpired";
    public const string TurnEnded = "TurnEnded";
    public const string OpponentDisconnected = "OpponentDisconnected";
    public const string OpponentReconnected = "OpponentReconnected";
    public const string GameOver = "GameOver";
    public const string Error = "Error";
}
