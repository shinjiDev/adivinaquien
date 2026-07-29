using System.Text.Json.Serialization;

namespace AdivinaQue.Contracts.ContentPack;

public sealed record AttributeValueDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("etiqueta")] string Etiqueta,
    [property: JsonPropertyName("pregunta")] string Pregunta,
    [property: JsonPropertyName("preguntaNegada")] string? PreguntaNegada = null,
    [property: JsonPropertyName("orden")] int? Orden = null);
