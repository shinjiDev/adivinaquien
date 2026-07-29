using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdivinaQue.Contracts.ContentPack;

public sealed record CardDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("imagen")] string Imagen,
    [property: JsonPropertyName("atributos")] IReadOnlyDictionary<string, JsonElement> Atributos,
    [property: JsonPropertyName("ficha")] string? Ficha = null,
    [property: JsonPropertyName("fuente")] string? Fuente = null);
