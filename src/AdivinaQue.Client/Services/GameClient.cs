using AdivinaQue.Contracts.Realtime;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace AdivinaQue.Client.Services;

/// <summary>
/// Contenedor de estado central: dueño del HubConnection, wrappea las acciones del
/// GameHub, y expone <see cref="Changed"/> para que los componentes se suscriban y
/// llamen StateHasChanged() — el patrón idiomático de Blazor, sin librería de estado
/// externa (anti-objetivo del spec).
/// </summary>
public sealed class GameClient : IAsyncDisposable
{
    private readonly PlayerIdentity _identity;
    private readonly NavigationManager _navigation;
    private HubConnection? _connection;
    private TaskCompletionSource<bool>? _pendingRoomAttempt;
    private long _lastStateVersion = -1;

    public GameClient(PlayerIdentity identity, NavigationManager navigation)
    {
        _identity = identity;
        _navigation = navigation;
    }

    public event Action? Changed;

    public Guid? PlayerId { get; private set; }

    public string? RoomCode { get; private set; }

    public IReadOnlyList<Guid> RoomPlayerIds { get; private set; } = Array.Empty<Guid>();

    public ProjectionDto? Projection { get; private set; }

    public ErrorDto? LastError { get; private set; }

    public double? OpponentDisconnectSecondsRemaining { get; private set; }

    public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;

    public async Task ConnectAsync()
    {
        if (_connection is not null)
        {
            return;
        }

        PlayerId = await _identity.GetOrCreateAsync();

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(_navigation.BaseUri), "hub/game"))
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
            })
            .Build();

        connection.On<RoomUpdatedDto>(EventNames.RoomUpdated, dto =>
        {
            RoomCode = dto.Code;
            RoomPlayerIds = dto.PlayerIds;
            _pendingRoomAttempt?.TrySetResult(true);
            NotifyChanged();
        });

        connection.On<ProjectionDto>(EventNames.GameStarted, UpdateProjection);
        connection.On<ProjectionDto>(EventNames.StateSync, UpdateProjection);

        connection.On<double>(EventNames.OpponentDisconnected, seconds =>
        {
            OpponentDisconnectSecondsRemaining = seconds;
            NotifyChanged();
        });

        connection.On(EventNames.OpponentReconnected, () =>
        {
            OpponentDisconnectSecondsRemaining = null;
            NotifyChanged();
        });

        connection.On<ErrorDto>(EventNames.Error, dto =>
        {
            LastError = dto;
            _pendingRoomAttempt?.TrySetResult(false);
            NotifyChanged();
        });

        connection.Reconnecting += _ =>
        {
            NotifyChanged();
            return Task.CompletedTask;
        };

        connection.Reconnected += async _ =>
        {
            if (RoomCode is not null && PlayerId is not null)
            {
                await connection.InvokeAsync("JoinRoom", RoomCode, PlayerId.Value);
            }

            NotifyChanged();
        };

        connection.Closed += _ =>
        {
            NotifyChanged();
            return Task.CompletedTask;
        };

        _connection = connection;
        await connection.StartAsync();
    }

    public async Task<bool> CreateRoomAsync()
    {
        await ConnectAsync();
        _pendingRoomAttempt = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _connection!.InvokeAsync("CreateRoom", PlayerId);
        return await _pendingRoomAttempt.Task;
    }

    public async Task<bool> JoinRoomAsync(string code)
    {
        await ConnectAsync();

        // Ya estamos adjuntos a esta sala en ESTA misma conexión (por CreateRoom o un
        // JoinRoom anterior) — no hace falta re-invocar el Hub. No se exige que
        // Projection ya no sea null: eso recién llega vía StateSync después de
        // RoomUpdated, y esperar por eso acá causaba un segundo JoinRoom redundante
        // (p. ej. al montar Room.razor justo después de crear/unirse) que competía
        // con las acciones reales de la partida. La reconexión real sigue invocando
        // "JoinRoom" directo en el handler Reconnected, sin pasar por este atajo.
        if (RoomCode == code)
        {
            return true;
        }

        _pendingRoomAttempt = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _connection!.InvokeAsync("JoinRoom", code, PlayerId);
        return await _pendingRoomAttempt.Task;
    }

    public Task SetReadyAsync() => _connection!.InvokeAsync("SetReady");

    public Task AskQuestionAsync(Guid actionId, string text) =>
        _connection!.InvokeAsync("AskQuestion", actionId, text, (SuggestedFromDto?)null);

    public Task SubmitAnswerAsync(Guid actionId, AnswerDto answer) =>
        _connection!.InvokeAsync("SubmitAnswer", actionId, answer);

    public Task ToggleEliminationAsync(string cardId) => _connection!.InvokeAsync("ToggleElimination", cardId);

    public Task EndTurnAsync(Guid actionId) => _connection!.InvokeAsync("EndTurn", actionId);

    public Task MakeGuessAsync(Guid actionId, string cardId) => _connection!.InvokeAsync("MakeGuess", actionId, cardId);

    public Task RequestResyncAsync() => _connection!.InvokeAsync("RequestResync");

    public Task LeaveRoomAsync() => _connection!.InvokeAsync("LeaveRoom");

    public void ClearLastError()
    {
        LastError = null;
        NotifyChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    private void UpdateProjection(ProjectionDto dto)
    {
        Projection = dto;

        // Cada ProjectionDto ya es un snapshot completo, no un delta, así que un salto
        // de StateVersion no pierde datos por sí solo — igual se pide explícitamente en
        // el spec como red de seguridad ante mensajes perdidos. Un rejoin (p. ej. tras
        // una reconexión de transporte) puede reenviar legítimamente la MISMA versión
        // sin que haya pasado nada — eso no es un salto, así que solo se dispara resync
        // ante un salto hacia ADELANTE, y _lastStateVersion nunca retrocede.
        if (_lastStateVersion >= 0 && dto.StateVersion > _lastStateVersion + 1)
        {
            _ = RequestResyncAsync();
        }

        if (dto.StateVersion > _lastStateVersion)
        {
            _lastStateVersion = dto.StateVersion;
        }

        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();
}
