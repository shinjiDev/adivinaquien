using AdivinaQue.Engine;
using AdivinaQue.Engine.Abstractions;
using AdivinaQue.Server.Persistence;
using AdivinaQue.Server.Rooms;
using AdivinaQue.Server.Tests.TestSupport;
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

    [Fact]
    public async Task GetAsync_UnknownCode_ReturnsNull()
    {
        var store = CreateStore();

        (await store.GetAsync("ZZZZZZ")).Should().BeNull();
    }

    [Fact]
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

    [Fact]
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

    [Fact]
    public async Task DeleteAsync_RemovesRoom()
    {
        var store = CreateStore();
        await store.SaveAsync(new RoomRecord { Code = "DEL001", CreatedAt = DateTimeOffset.UtcNow, LastActivityAt = DateTimeOffset.UtcNow });

        await store.DeleteAsync("DEL001");

        (await store.GetAsync("DEL001")).Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllSavedRooms()
    {
        var store = CreateStore();
        await store.SaveAsync(new RoomRecord { Code = "ALL001", CreatedAt = DateTimeOffset.UtcNow, LastActivityAt = DateTimeOffset.UtcNow });
        await store.SaveAsync(new RoomRecord { Code = "ALL002", CreatedAt = DateTimeOffset.UtcNow, LastActivityAt = DateTimeOffset.UtcNow });

        var all = await store.GetAllAsync();

        all.Select(r => r.Code).Should().Contain(new[] { "ALL001", "ALL002" });
    }

    [Fact]
    public async Task SaveThenGet_RoundTripsMatchSnapshotFaithfully()
    {
        var store = CreateStore();
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = Match.Create(playerA, playerB, new[] { new Card("c1"), new Card("c2") }, clock, new SeededRandom(1));
        match.SetReady(playerA);
        match.SetReady(playerB);

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
