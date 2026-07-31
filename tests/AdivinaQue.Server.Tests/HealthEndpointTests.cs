using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AdivinaQue.Server.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Healthz_ReturnsOk_WhenGameStoreResponds()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
