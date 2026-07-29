using AdivinaQue.Server.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace AdivinaQue.Server.Tests;

public class QrEndpointTests : IAsyncLifetime
{
    private readonly ServerFixture _fixture = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _fixture.DisposeAsync().AsTask();

    [Fact]
    public async Task Get_ForUnknownRoom_ReturnsNotFound()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/rooms/ZZZZZZ/qr");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ForExistingRoom_ReturnsAValidPng()
    {
        var playerId = Guid.NewGuid();
        await using var conn = _fixture.CreateHubConnection();
        var collector = new HubEventCollector(conn);
        await conn.StartAsync();
        await conn.InvokeAsync("CreateRoom", playerId);
        var roomUpdated = await HubEventCollector.WaitAsync(collector.RoomUpdated.Reader);

        using var client = _fixture.CreateClient();
        var response = await client.GetAsync($"/rooms/{roomUpdated.Code}/qr");

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();

        // Firma PNG: 89 50 4E 47 0D 0A 1A 0A
        bytes.Take(8).Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
    }
}
