using AdivinaQue.Engine;

namespace AdivinaQue.Server.Rooms;

/// <summary>
/// Fuente del mazo con el que se crea cada <see cref="Match"/>. La carga real de un
/// content pack (Fase 2) hacia un mazo del motor es trabajo de Fase 4/5/6 — por ahora
/// <see cref="PlaceholderDeckProvider"/> entrega un mazo sintético para que el Server
/// sea funcional y testeable end-to-end sin esperar a que exista un pack real.
/// </summary>
public interface IDeckProvider
{
    IReadOnlyList<Card> GetDeck();
}

public sealed class PlaceholderDeckProvider : IDeckProvider
{
    private static readonly IReadOnlyList<Card> Deck = Enumerable.Range(0, 16)
        .Select(i => new Card($"card-{i}"))
        .ToList();

    public IReadOnlyList<Card> GetDeck() => Deck;
}
