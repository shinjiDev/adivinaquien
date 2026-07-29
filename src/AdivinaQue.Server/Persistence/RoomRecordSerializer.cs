using System.Text.Json;
using System.Text.Json.Serialization;
using AdivinaQue.Server.Rooms;

namespace AdivinaQue.Server.Persistence;

/// <summary>
/// Serialización compartida por ambas implementaciones de <see cref="IGameStore"/>.
/// <see cref="InMemoryGameStore"/> también pasa por JSON (no guarda la referencia viva)
/// para que las dos implementaciones tengan exactamente la misma semántica de
/// aislamiento: mutar el objeto devuelto por <c>GetAsync</c> sin volver a llamar
/// <c>SaveAsync</c> nunca afecta lo persistido.
/// </summary>
internal static class RoomRecordSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(RoomRecord record) => JsonSerializer.Serialize(record, Options);

    public static RoomRecord Deserialize(string json) =>
        JsonSerializer.Deserialize<RoomRecord>(json, Options)
        ?? throw new JsonException("RoomRecord deserializó a null.");
}
