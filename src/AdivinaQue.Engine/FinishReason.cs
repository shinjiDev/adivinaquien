namespace AdivinaQue.Engine;

public enum FinishReason
{
    CorrectGuess,
    WrongGuess,
    Forfeit,

    /// <summary>
    /// Reservado por la máquina de estados del spec original; ningún flujo de Fase 1
    /// lo produce (el único timeout implementado, el de 60s en AwaitingAnswer, no
    /// termina la partida). Ver nota en el resumen de Fase 1.
    /// </summary>
    Timeout,
}
