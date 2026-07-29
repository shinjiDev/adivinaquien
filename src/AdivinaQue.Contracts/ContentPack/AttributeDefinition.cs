using System.Text.Json.Serialization;

namespace AdivinaQue.Contracts.ContentPack;

public sealed record AttributeDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("tipo")] AttributeType Tipo,
    [property: JsonPropertyName("etiqueta")] string Etiqueta,
    [property: JsonPropertyName("pregunta")] string? Pregunta = null,
    [property: JsonPropertyName("preguntaNegada")] string? PreguntaNegada = null,
    [property: JsonPropertyName("valores")] IReadOnlyList<AttributeValueDefinition>? Valores = null);
