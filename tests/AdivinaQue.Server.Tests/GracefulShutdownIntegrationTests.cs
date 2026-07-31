using AdivinaQue.Server.BackgroundServices;
using AdivinaQue.Server.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AdivinaQue.Server.Tests;

/// <summary>
/// Con autoescalado a cero el proceso puede terminar en cualquier momento sin tráfico,
/// no solo en un deploy — GracefulShutdownService.StopAsync se llama directamente (mismo
/// patrón que RoomActivityMonitor.SweepOnceAsync) en vez de apagar todo el
/// WebApplicationFactory, que sería frágil para verificar la entrega del evento por
/// SignalR antes de que la conexión misma se caiga.
/// </summary>
public class GracefulShutdownIntegrationTests : IAsyncLifetime
{
    private readonly ServerFixture _fixture = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _fixture.DisposeAsync().AsTask();

    [Fact]
    public async Task StopAsync_NotifiesAllConnectedPlayersInActiveRooms()
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();

        await using var connA = _fixture.CreateHubConnection();
        await using var connB = _fixture.CreateHubConnection();
        var (_, a, b, _) = await GameplaySetup.CreateReadyGameAsync(connA, connB, playerA, playerB);

        var shutdownService = _fixture.Services.GetRequiredService<GracefulShutdownService>();
        await shutdownService.StopAsync(CancellationToken.None);

        (await HubEventCollector.WaitAsync(a.ServerShuttingDown.Reader)).Should().BeTrue();
        (await HubEventCollector.WaitAsync(b.ServerShuttingDown.Reader)).Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_WithNoActiveRooms_CompletesWithoutError()
    {
        var shutdownService = _fixture.Services.GetRequiredService<GracefulShutdownService>();

        var act = async () => await shutdownService.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
