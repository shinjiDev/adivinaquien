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

    // Con autoescalado a cero (Container Apps), el proceso puede terminar en cualquier
    // momento en que no haya tráfico activo — no solo en un deploy. Este evento avisa a
    // los clientes conectados justo antes del apagado ordenado, para que la
    // desconexión que sigue se vea como lo que es (el server bajando a propósito) y no
    // como un error de red — el cliente ya reconecta solo vía WithAutomaticReconnect.
    public const string ServerShuttingDown = "ServerShuttingDown";
}
