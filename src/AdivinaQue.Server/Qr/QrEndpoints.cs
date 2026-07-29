using AdivinaQue.Server.Persistence;
using QRCoder;

namespace AdivinaQue.Server.Qr;

public static class QrEndpoints
{
    public static IEndpointRouteBuilder MapQrEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/rooms/{code}/qr", async (string code, HttpRequest request, IGameStore store, CancellationToken ct) =>
        {
            var room = await store.GetAsync(code, ct);
            if (room is null)
            {
                return Results.NotFound();
            }

            var deepLink = $"{request.Scheme}://{request.Host}/join/{code}";

            // PngByteQRCode es puramente managed (a diferencia de QRCode, que usa
            // System.Drawing) para que funcione igual en el contenedor Linux del
            // Dockerfile que en desarrollo local en Windows.
            using var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(deepLink, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data);
            var bytes = png.GetGraphic(20);

            return Results.File(bytes, "image/png");
        });

        return app;
    }
}
