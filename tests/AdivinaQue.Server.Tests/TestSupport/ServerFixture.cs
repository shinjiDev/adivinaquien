using AdivinaQue.Engine.Abstractions;
using AdivinaQue.Server.BackgroundServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AdivinaQue.Server.Tests.TestSupport;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> con el <see cref="IClock"/>
/// reemplazado por un <see cref="FakeClock"/> controlable, para que los tests de
/// timeouts avancen el tiempo instantáneamente en vez de esperar segundos reales. El
/// barrido automático de <see cref="RoomActivityMonitor"/> queda con un intervalo de
/// una hora (nunca dispara solo durante la vida de un test); los tests llaman
/// <see cref="GetMonitor"/>().SweepOnceAsync() directamente para un barrido determinista.
/// </summary>
public sealed class ServerFixture : WebApplicationFactory<Program>
{
    public FakeClock Clock { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Room:SweepIntervalSeconds"] = "3600",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }

    public HubConnection CreateHubConnection() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "hub/game"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
            })
            .Build();

    public RoomActivityMonitor GetMonitor() => Services.GetRequiredService<RoomActivityMonitor>();
}
