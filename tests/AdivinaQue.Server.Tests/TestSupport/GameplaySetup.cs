using AdivinaQue.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR.Client;

namespace AdivinaQue.Server.Tests.TestSupport;

/// <summary>Boilerplate compartido: crear sala, unir al segundo jugador, marcar Ready
/// ambos, y esperar a que el motor pase a InTurn — usado por todos los tests de
/// integración que necesitan una partida ya arrancada como punto de partida.</summary>
public static class GameplaySetup
{
    public static async Task<(string Code, HubEventCollector A, HubEventCollector B, Guid ActivePlayerId)> CreateReadyGameAsync(
        HubConnection connA,
        HubConnection connB,
        Guid playerA,
        Guid playerB)
    {
        var a = new HubEventCollector(connA);
        var b = new HubEventCollector(connB);

        await connA.StartAsync();
        await connB.StartAsync();

        await connA.InvokeAsync("CreateRoom", playerA);
        var roomUpdated = await HubEventCollector.WaitAsync(a.RoomUpdated.Reader);
        var code = roomUpdated.Code;

        await connB.InvokeAsync("JoinRoom", code, playerB);
        await HubEventCollector.WaitForAsync(b.RoomUpdated.Reader, r => r.PlayerIds.Count == 2);

        await connA.InvokeAsync("SetReady");
        await connB.InvokeAsync("SetReady");

        var startedA = await HubEventCollector.WaitForAsync(a.GameStarted.Reader, s => s.Status == GameStatusDto.InTurn);
        await HubEventCollector.WaitForAsync(b.GameStarted.Reader, s => s.Status == GameStatusDto.InTurn);

        return (code, a, b, startedA.ActivePlayerId!.Value);
    }
}
