using AdivinaQue.Server.Rooms;
using Microsoft.Data.Sqlite;

namespace AdivinaQue.Server.Persistence;

/// <summary>
/// Sin ORM (anti-objetivo del spec): SQL crudo sobre <c>Microsoft.Data.Sqlite</c>, una
/// tabla, <c>data</c> es el JSON de <see cref="RoomRecord"/>. Mantiene una única
/// conexión abierta durante toda la vida del store (necesario para que
/// <c>Data Source=:memory:</c> no se borre entre llamadas) serializada con un
/// semáforo, porque <see cref="SqliteConnection"/> no es segura para uso concurrente.
/// </summary>
public sealed class SqliteGameStore : IGameStore, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteGameStore(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        Initialize();
    }

    public async Task<RoomRecord?> GetAsync(string code, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT data FROM rooms WHERE code = $code";
            command.Parameters.AddWithValue("$code", code);

            var result = await command.ExecuteScalarAsync(ct);
            return result is string json ? RoomRecordSerializer.Deserialize(json) : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(RoomRecord room, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO rooms (code, data, last_activity)
                VALUES ($code, $data, $lastActivity)
                ON CONFLICT(code) DO UPDATE SET data = excluded.data, last_activity = excluded.last_activity;
                """;
            command.Parameters.AddWithValue("$code", room.Code);
            command.Parameters.AddWithValue("$data", RoomRecordSerializer.Serialize(room));
            command.Parameters.AddWithValue("$lastActivity", room.LastActivityAt.ToString("O"));

            await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(string code, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM rooms WHERE code = $code";
            command.Parameters.AddWithValue("$code", code);

            await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<RoomRecord>> GetAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT data FROM rooms";

            var results = new List<RoomRecord>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(RoomRecordSerializer.Deserialize(reader.GetString(0)));
            }

            return results;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
        _lock.Dispose();
    }

    private void Initialize()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS rooms (
                code TEXT PRIMARY KEY,
                data TEXT NOT NULL,
                last_activity TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
