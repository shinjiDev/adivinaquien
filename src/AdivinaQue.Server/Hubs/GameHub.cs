using AdivinaQue.Contracts.Realtime;
using AdivinaQue.Engine;
using AdivinaQue.Server.Mapping;
using AdivinaQue.Server.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace AdivinaQue.Server.Hubs;

public sealed class GameHub : Hub
{
    private readonly RoomService _rooms;
    private readonly ConnectionRegistry _connections;
    private readonly GameEventPublisher _events;
    private readonly MatchOptions _matchOptions;

    public GameHub(RoomService rooms, ConnectionRegistry connections, GameEventPublisher events, MatchOptions matchOptions)
    {
        _rooms = rooms;
        _connections = connections;
        _events = events;
        _matchOptions = matchOptions;
    }

    public async Task CreateRoom(Guid playerId)
    {
        var code = await _rooms.CreateRoomAsync(playerId);
        await AttachAsync(code, playerId);
        await _events.PushToRoomAsync(code, EventNames.RoomUpdated, new RoomUpdatedDto(code, new[] { playerId }));
    }

    public async Task JoinRoom(string code, Guid playerId)
    {
        var result = await _rooms.JoinRoomAsync(code, playerId);
        if (!result.IsSuccess)
        {
            await SendErrorAsync(result);
            return;
        }

        await AttachAsync(code, playerId);

        var room = result.Room!;
        var playerIds = new List<Guid>();
        if (room.PlayerA is not null)
        {
            playerIds.Add(room.PlayerA.Value);
        }

        if (room.PlayerB is not null)
        {
            playerIds.Add(room.PlayerB.Value);
        }

        await _events.PushToRoomAsync(code, EventNames.RoomUpdated, new RoomUpdatedDto(code, playerIds));

        if (result.WasReconnected)
        {
            await _events.PushToRoomAsync(code, EventNames.OpponentReconnected, null);
        }

        if (result.Match is not null)
        {
            await _events.PushStateSyncAsync(code, result.Match);
        }
    }

    public Task SetReady()
    {
        if (!TryGetConnection(out var info))
        {
            return SendInvalidRequestAsync();
        }

        return RunActionAsync(info.RoomCode, m => m.SetReady(info.PlayerId));
    }

    public Task AskQuestion(Guid actionId, string text, SuggestedFromDto? suggestedFrom)
    {
        if (!TryGetConnection(out var info))
        {
            return SendInvalidRequestAsync();
        }

        return RunActionAsync(
            info.RoomCode,
            m => m.AskQuestion(actionId, info.PlayerId, text, ProjectionMapper.ToEngine(suggestedFrom)),
            EventNames.QuestionAsked,
            text);
    }

    public Task SubmitAnswer(Guid actionId, AnswerDto answer)
    {
        if (!TryGetConnection(out var info))
        {
            return SendInvalidRequestAsync();
        }

        return RunActionAsync(
            info.RoomCode,
            m => m.SubmitAnswer(actionId, info.PlayerId, ProjectionMapper.ToEngine(answer)),
            EventNames.AnswerGiven,
            answer);
    }

    public Task ToggleElimination(string cardId)
    {
        if (!TryGetConnection(out var info))
        {
            return SendInvalidRequestAsync();
        }

        return RunActionAsync(info.RoomCode, m => m.ToggleElimination(info.PlayerId, cardId));
    }

    public Task EndTurn(Guid actionId)
    {
        if (!TryGetConnection(out var info))
        {
            return SendInvalidRequestAsync();
        }

        return RunActionAsync(info.RoomCode, m => m.EndTurn(actionId, info.PlayerId), EventNames.TurnEnded, null);
    }

    public Task MakeGuess(Guid actionId, string cardId)
    {
        if (!TryGetConnection(out var info))
        {
            return SendInvalidRequestAsync();
        }

        return RunActionAsync(info.RoomCode, m => m.MakeGuess(actionId, info.PlayerId, cardId));
    }

    public async Task RequestResync()
    {
        if (!TryGetConnection(out var info))
        {
            await SendInvalidRequestAsync();
            return;
        }

        var match = await _rooms.GetLiveMatchAsync(info.RoomCode);
        if (match is not null)
        {
            await _events.PushStateSyncAsync(info.RoomCode, match);
        }
    }

    public Task LeaveRoom()
    {
        if (!TryGetConnection(out var info))
        {
            return SendInvalidRequestAsync();
        }

        return RunActionAsync(info.RoomCode, () => _rooms.LeaveAsync(info.RoomCode, info.PlayerId));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetConnection(out var info))
        {
            _connections.Remove(Context.ConnectionId);
            var secondsRemaining = _matchOptions.DisconnectGrace.TotalSeconds;
            await RunActionAsync(
                info.RoomCode,
                m => m.Disconnect(info.PlayerId),
                EventNames.OpponentDisconnected,
                secondsRemaining);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task AttachAsync(string code, Guid playerId)
    {
        _connections.Register(Context.ConnectionId, code, playerId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GameEventPublisher.RoomGroup(code));
        await Groups.AddToGroupAsync(Context.ConnectionId, GameEventPublisher.PlayerGroup(code, playerId));
    }

    private bool TryGetConnection(out PlayerConnection info) => _connections.TryGet(Context.ConnectionId, out info);

    private Task RunActionAsync(string code, Func<Match, Result> action, string? eventName = null, object? eventPayload = null) =>
        RunActionAsync(code, () => _rooms.ExecuteActionAsync(code, action), eventName, eventPayload);

    private async Task RunActionAsync(
        string code,
        Func<Task<RoomActionResult>> operation,
        string? eventName = null,
        object? eventPayload = null)
    {
        var result = await operation();

        if (!result.IsSuccess)
        {
            await SendErrorAsync(result);
            return;
        }

        if (eventName is not null)
        {
            await _events.PushToRoomAsync(code, eventName, eventPayload);
        }

        if (result.Match is not null)
        {
            var justStarted = result.StatusBefore == GameStatus.Lobby && result.Match.Status == GameStatus.InTurn;
            await _events.PushStateSyncAsync(code, result.Match, justStarted ? EventNames.GameStarted : EventNames.StateSync);

            if (result.Match.Status == GameStatus.Finished)
            {
                await _events.PushToRoomAsync(code, EventNames.GameOver, null);
            }
        }
    }

    private async Task SendErrorAsync(RoomActionResult result)
    {
        var wireCode = result.RoomError is { } roomError
            ? ProjectionMapper.ToWireError(roomError)
            : ProjectionMapper.ToWireError(result.EngineError!.Value);

        await _events.PushErrorAsync(Context.ConnectionId, new ErrorDto(wireCode, wireCode.ToString()));
    }

    private Task SendInvalidRequestAsync() =>
        _events.PushErrorAsync(Context.ConnectionId, new ErrorDto(WireErrorCode.InvalidRequest, "No estás en ninguna sala."));
}
