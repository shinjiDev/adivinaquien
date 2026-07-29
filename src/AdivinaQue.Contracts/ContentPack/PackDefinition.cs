using System.Text.Json.Serialization;

namespace AdivinaQue.Contracts.ContentPack;

public sealed record PackDefinition(
    [property: JsonPropertyName("packId")] string PackId,
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("descripcion")] string Descripcion,
    [property: JsonPropertyName("idioma")] string Idioma,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("atributos")] IReadOnlyList<AttributeDefinition> Atributos,
    [property: JsonPropertyName("cartas")] IReadOnlyList<CardDefinition> Cartas);
