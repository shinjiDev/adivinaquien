using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdivinaQue.Server.Tests;

/// <summary>
/// Verifica la configuración exacta de ForwardedHeadersOptions usada en Program.cs
/// (flags + KnownNetworks/KnownProxies limpiados) contra un host mínimo dedicado, en vez
/// de decodificar el PNG del QR real — lo que puede fallar si la config está mal es
/// justo esto (los flags, el orden en la tubería), no el middleware de ASP.NET Core en
/// sí (ya probado por el framework). Esta es la razón por la que existe la Fase 1 del
/// despliegue: detrás del ingress de Container Apps, sin esto, QrEndpoints.cs armaría el
/// deep link con el scheme/host internos del contenedor en vez de los públicos.
/// </summary>
public class ForwardedHeadersTests
{
    [Fact]
    public async Task ForwardedProtoAndHost_OverrideSchemeAndHostSeenByAppCode()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
                        options.KnownNetworks.Clear();
                        options.KnownProxies.Clear();
                    });
                });
                webBuilder.Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.Run(context => context.Response.WriteAsync($"{context.Request.Scheme}://{context.Request.Host}"));
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "adivinaquien.example.com");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Be("https://adivinaquien.example.com");
    }

    [Fact]
    public async Task WithoutForwardedHeaders_KeepsTheDirectRequestSchemeAndHost()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
                        options.KnownNetworks.Clear();
                        options.KnownProxies.Clear();
                    });
                });
                webBuilder.Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.Run(context => context.Response.WriteAsync($"{context.Request.Scheme}://{context.Request.Host}"));
                });
            })
            .StartAsync();

        var client = host.GetTestClient();

        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        // TestServer sin headers de reenvío: se mantiene el scheme/host directos de la
        // request (http://localhost, el default del TestServer) — confirma que el
        // middleware no inventa nada cuando no hay cabeceras que reenviar.
        body.Should().Be("http://localhost");
    }
}
