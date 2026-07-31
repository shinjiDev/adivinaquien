using System.Net.Sockets;
using AdivinaQue.Engine;
using AdivinaQue.Engine.Abstractions;
using AdivinaQue.Server.Persistence;
using AdivinaQue.Server.Rooms;
using AdivinaQue.Server.Tests.TestSupport;
using Azure.Data.Tables;
using FluentAssertions;

namespace AdivinaQue.Server.Tests;

/// <summary>
/// Mismo contrato ejercitado contra ambas implementaciones de <see cref="IGameStore"/>
/// (ver las dos clases concretas al final del archivo) — no basta con que ambas
/// compilen contra la interfaz, deben comportarse igual.
/// </summary>
public abstract class GameStoreContractTests
{
    protected abstract IGameStore CreateStore();

    [SkippableFact]
    public async Task GetAsync_UnknownCode_ReturnsNull()
    {
        var store = CreateStore();

        (await store.GetAsync("ZZZZZZ")).Should().BeNull();
    }

    [SkippableFact]
    public async Task SaveThenGet_ReturnsIsolatedCopy()
    {
        var store = CreateStore();
        var room = new RoomRecord { Code = "ABC123", CreatedAt = DateTimeOffset.UtcNow, LastActivityAt = DateTimeOffset.UtcNow, PlayerA = Guid.NewGuid() };
        await store.SaveAsync(room);

        var loaded = await store.GetAsync("ABC123");
        loaded.Should().NotBeNull();
        loaded!.PlayerA.Should().Be(room.PlayerA);

        loaded.PlayerB = Guid.NewGuid();
        var reloaded = await store.GetAsync("ABC123");
        reloaded!.PlayerB.Should().BeNull("mutar la copia leída sin volver a Save no debe afectar lo persistido");
    }

    [SkippableFact]
    public async Task Save_Twice_OverwritesPreviousValue()
    {
        var store = CreateStore();
        var room = new RoomRecord { Code = "OVR001", CreatedAt = DateTimeOffset.UtcNow, LastActivityAt = DateTimeOffset.UtcNow, PlayerA = Guid.NewGuid() };
        await store.SaveAsync(room);

        room.PlayerB = Guid.NewGuid();
        await store.SaveAsync(room);

        var loaded = await store.GetAsync("OVR001");
        loaded!.PlayerB.Should().Be(room.PlayerB);
    }

    [SkippableFact]
    public async Task DeleteAsync_RemovesRoom()
    {
        var store = CreateStore();
        await store.SaveAsync(new RoomRecord { Code = "DEL001", CreatedAt = DateTimeOffset.UtcNow, LastActivityAt = DateTimeOffset.UtcNow });

        await store.DeleteAsync("DEL001");

        (await store.GetAsync("DEL001")).Should().BeNull();
    }

    [SkippableFact]
    public async Task GetAllAsync_ReturnsAllSavedRooms()
    {
        var store = CreateStore();
        await store.SaveAsync(new RoomRecord { Code = "ALL001", CreatedAt = DateTimeOffset.UtcNow, LastActivityAt = DateTimeOffset.UtcNow });
        await store.SaveAsync(new RoomRecord { Code = "ALL002", CreatedAt = DateTimeOffset.UtcNow, LastActivityAt = DateTimeOffset.UtcNow });

        var all = await store.GetAllAsync();

        all.Select(r => r.Code).Should().Contain(new[] { "ALL001", "ALL002" });
    }

    [SkippableFact]
    public async Task PingAsync_DoesNotThrow_WhenStoreIsReachable()
    {
        var store = CreateStore();

        var act = async () => await store.PingAsync();

        await act.Should().NotThrowAsync();
    }

    [SkippableFact]
    public async Task SaveThenGet_RoundTripsMatchSnapshotFaithfully()
    {
        var store = CreateStore();
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = Match.Create(playerA, playerB, new[] { new Card("c1"), new Card("c2") }, clock, new SeededRandom(1));
        match.SetReady(playerA);
        match.SetReady(playerB);
        match.ChooseCharacter(playerA, "c1");
        match.ChooseCharacter(playerB, "c2");

        var room = new RoomRecord
        {
            Code = "SNAP01",
            CreatedAt = clock.UtcNow,
            LastActivityAt = clock.UtcNow,
            PlayerA = playerA,
            PlayerB = playerB,
            Match = match.ToSnapshot(),
        };
        await store.SaveAsync(room);

        var loaded = await store.GetAsync("SNAP01");
        loaded!.Match.Should().NotBeNull();

        var restored = Match.FromSnapshot(loaded.Match!, clock, new SeededRandom(1));
        restored.Status.Should().Be(GameStatus.InTurn);
        restored.GetSecretCard(playerA).Should().Be(match.GetSecretCard(playerA));
        restored.GetSecretCard(playerB).Should().Be(match.GetSecretCard(playerB));
    }
}

public sealed class InMemoryGameStoreContractTests : GameStoreContractTests
{
    protected override IGameStore CreateStore() => new InMemoryGameStore();
}

public sealed class SqliteGameStoreContractTests : GameStoreContractTests, IDisposable
{
    private readonly SqliteGameStore _store = new("Data Source=:memory:");

    protected override IGameStore CreateStore() => _store;

    public void Dispose() => _store.Dispose();
}

/// <summary>
/// Corre el mismo contrato contra Azurite (emulador local de Azure Storage) en vez de
/// contra la nube real — así el mismo IGameStore que se usa en Container Apps
/// (Storage:Provider=Table) queda probado de verdad, no solo compilado contra la
/// interfaz. Requiere `azurite` corriendo en los puertos default (10000-10002); si no
/// está disponible el fixture salta la clase entera con un mensaje explícito en vez de
/// fallar de forma confusa.
/// </summary>
public sealed class TableStorageGameStoreContractTests : GameStoreContractTests
{
    // Connection string bien conocida y pública del emulador Azurite — no es un secreto
    // real, es la misma para cualquier instalación de Azurite en cualquier máquina.
    private const string AzuriteConnectionString =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";

    protected override IGameStore CreateStore()
    {
        Skip.IfNot(IsAzuriteReachable(), "Azurite no está corriendo en 127.0.0.1:10002 — " +
            "instálalo con 'npm install -g azurite' y arráncalo con " +
            "'azurite --skipApiVersionCheck --location <carpeta>' para correr estos tests.");

        return new TableStorageGameStore(new TableServiceClient(AzuriteConnectionString));
    }

    private static bool IsAzuriteReachable()
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("127.0.0.1", 10002).Wait(TimeSpan.FromMilliseconds(500));
        }
        catch
        {
            return false;
        }
    }
}
