using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdivinaQue.Contracts.ContentPack;

public static class PackParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static PackDefinition Parse(string json)
    {
        return JsonSerializer.Deserialize<PackDefinition>(json, Options)
            ?? throw new JsonException("El pack deserializó a null.");
    }

    public static string Serialize(PackDefinition pack) => JsonSerializer.Serialize(pack, Options);
}
