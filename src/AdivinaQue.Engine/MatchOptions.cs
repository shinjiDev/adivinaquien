namespace AdivinaQue.Engine;

public sealed class MatchOptions
{
    public WrongGuessPolicy WrongGuessPolicy { get; init; } = WrongGuessPolicy.EndsMatch;

    public TimeSpan AnswerTimeout { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan DisconnectGrace { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Desactivado por defecto (null), tal como pide el spec. Reservado para un futuro
    /// temporizador de turno; Fase 1 no implementa su efecto de expiración porque el
    /// spec no lo describe.
    /// </summary>
    public TimeSpan? TurnTimeout { get; init; }
}
