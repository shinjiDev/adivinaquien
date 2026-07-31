using Azure;
using Azure.Data.Tables;
using AdivinaQue.Server.Rooms;

namespace AdivinaQue.Server.Persistence;

/// <summary>
/// Backing real de producción en Azure Container Apps (ver Fase 1 del despliegue).
/// Mismo patrón "un blob de JSON por sala" que <see cref="SqliteGameStore"/>
/// (<see cref="RoomRecordSerializer"/> es compartido) — acá la fila es una entidad de
/// Table Storage en vez de una fila SQL. Todas las salas viven en una sola partición
/// ("room"): el volumen esperado (unas pocas partidas concurrentes) está muy por debajo
/// de donde el particionado por otra clave empezaría a importar para rendimiento, y una
/// sola partición simplifica <see cref="GetAllAsync"/> a una sola query.
///
/// La autenticación (managed identity vs. connection string de Azurite en tests/local)
/// es responsabilidad de quien construye el <see cref="TableServiceClient"/> que se
/// pasa acá — este tipo no sabe ni le importa cómo se autenticó.
/// </summary>
public sealed class TableStorageGameStore : IGameStore
{
    private const string TableName = "rooms";
    private const string PartitionKeyValue = "room";
    private const string DataPropertyName = "Data";

    private readonly TableServiceClient _serviceClient;
    private readonly TableClient _table;

    public TableStorageGameStore(TableServiceClient serviceClient)
    {
        _serviceClient = serviceClient;
        _table = serviceClient.GetTableClient(TableName);
        _table.CreateIfNotExists();
    }

    public async Task<RoomRecord?> GetAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var response = await _table.GetEntityAsync<TableEntity>(PartitionKeyValue, code, cancellationToken: ct);
            return Deserialize(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveAsync(RoomRecord room, CancellationToken ct = default)
    {
        var entity = new TableEntity(PartitionKeyValue, room.Code)
        {
            [DataPropertyName] = RoomRecordSerializer.Serialize(room),
        };

        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task DeleteAsync(string code, CancellationToken ct = default)
    {
        try
        {
            await _table.DeleteEntityAsync(PartitionKeyValue, code, cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Ya no existía — DeleteAsync es idempotente en las otras implementaciones
            // de IGameStore, así que esto no debe ser un error acá tampoco.
        }
    }

    public async Task<IReadOnlyList<RoomRecord>> GetAllAsync(CancellationToken ct = default)
    {
        var results = new List<RoomRecord>();

        await foreach (var entity in _table.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{PartitionKeyValue}'",
            cancellationToken: ct))
        {
            results.Add(Deserialize(entity));
        }

        return results;
    }

    public async Task PingAsync(CancellationToken ct = default)
    {
        // Confirma que el servicio responde (red + autenticación) sin tocar datos de
        // ninguna sala — justo lo que necesita /healthz.
        await _serviceClient.GetPropertiesAsync(ct);
    }

    private static RoomRecord Deserialize(TableEntity entity) =>
        RoomRecordSerializer.Deserialize(entity.GetString(DataPropertyName)
            ?? throw new InvalidOperationException($"La entidad '{entity.RowKey}' no tiene la propiedad '{DataPropertyName}'."));
}
