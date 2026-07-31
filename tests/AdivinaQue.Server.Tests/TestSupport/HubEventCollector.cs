using System.Threading.Channels;
using AdivinaQue.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR.Client;

namespace AdivinaQue.Server.Tests.TestSupport;

/// <summary>
/// Envuelve un <see cref="HubConnection"/> y bufferea cada evento servidor→cliente en
/// un <see cref="Channel{T}"/> propio, para que un test pueda hacer
/// <c>await WaitAsync(collector.StateSyncs)</c> después de cada acción y recibir
/// exactamente el próximo evento de ese tipo, en orden, sin condiciones de carrera.
/// </summary>
public sealed class HubEventCollector
{
    public HubConnection Connection { get; }

    public Channel<ProjectionDto> StateSyncs { get; } = Channel.CreateUnbounded<ProjectionDto>();

    public Channel<ProjectionDto> GameStarted { get; } = Channel.CreateUnbounded<ProjectionDto>();

    public Channel<RoomUpdatedDto> RoomUpdated { get; } = Channel.CreateUnbounded<RoomUpdatedDto>();

    public Channel<ErrorDto> Errors { get; } = Channel.CreateUnbounded<ErrorDto>();

    public Channel<bool> GameOvers { get; } = Channel.CreateUnbounded<bool>();

    public Channel<bool> QuestionExpired { get; } = Channel.CreateUnbounded<bool>();

    public Channel<double> OpponentDisconnected { get; } = Channel.CreateUnbounded<double>();

    public Channel<bool> OpponentReconnected { get; } = Channel.CreateUnbounded<bool>();

    public Channel<bool> ServerShuttingDown { get; } = Channel.CreateUnbounded<bool>();

    public HubEventCollector(HubConnection connection)
    {
        Connection = connection;
        connection.On<ProjectionDto>(EventNames.StateSync, dto => StateSyncs.Writer.TryWrite(dto));
        connection.On<ProjectionDto>(EventNames.GameStarted, dto => GameStarted.Writer.TryWrite(dto));
        connection.On<RoomUpdatedDto>(EventNames.RoomUpdated, dto => RoomUpdated.Writer.TryWrite(dto));
        connection.On<ErrorDto>(EventNames.Error, dto => Errors.Writer.TryWrite(dto));
        connection.On(EventNames.GameOver, () => GameOvers.Writer.TryWrite(true));
        connection.On(EventNames.QuestionExpired, () => QuestionExpired.Writer.TryWrite(true));
        connection.On<double>(EventNames.OpponentDisconnected, secs => OpponentDisconnected.Writer.TryWrite(secs));
        connection.On(EventNames.OpponentReconnected, () => OpponentReconnected.Writer.TryWrite(true));
        connection.On(EventNames.ServerShuttingDown, () => ServerShuttingDown.Writer.TryWrite(true));
    }

    public static async Task<T> WaitAsync<T>(ChannelReader<T> reader, TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        return await reader.ReadAsync(cts.Token);
    }

    /// <summary>
    /// Lee del canal hasta encontrar un elemento que cumpla <paramref name="predicate"/>,
    /// descartando los intermedios. El Hub puede emitir StateSync "de paso" (p. ej. el
    /// Lobby mientras se espera al segundo Ready) que no son el que un test puntual está
    /// esperando — leer secuencialmente sin filtrar acopla el test al conteo exacto de
    /// mensajes, que es un detalle de implementación frágil.
    /// </summary>
    public static async Task<T> WaitForAsync<T>(ChannelReader<T> reader, Func<T, bool> predicate, TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        while (true)
        {
            var item = await reader.ReadAsync(cts.Token);
            if (predicate(item))
            {
                return item;
            }
        }
    }
}
