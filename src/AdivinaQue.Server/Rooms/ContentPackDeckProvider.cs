using AdivinaQue.Contracts.ContentPack;
using AdivinaQue.Engine;

namespace AdivinaQue.Server.Rooms;

/// <summary>
/// Carga un content pack real (Fase 2) desde disco y lo convierte al mazo del motor.
/// <c>Imagen</c> se reescribe con el prefijo del propio pack (p. ej. "characters/img/x.png")
/// porque el Server monta TODO <c>content/</c> como archivos estáticos bajo <c>/content</c>
/// (ver <c>Program.cs</c>) — así el cliente arma la URL final con un solo prefijo fijo,
/// sin importar qué pack esté activo.
/// </summary>
public sealed class ContentPackDeckProvider : IDeckProvider
{
    private readonly IReadOnlyList<Card> _deck;

    public ContentPackDeckProvider(string contentRootDirectory, string packId)
    {
        var packPath = Path.Combine(contentRootDirectory, packId, "pack.json");
        var json = File.ReadAllText(packPath);
        var definition = PackParser.Parse(json);

        _deck = definition.Cartas
            .Select(card => new Card(card.Id, card.Nombre, $"{packId}/{card.Imagen}", card.Ficha ?? ""))
            .ToList();
    }

    public IReadOnlyList<Card> GetDeck() => _deck;
}
