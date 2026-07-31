using AdivinaQue.Engine.Abstractions;

namespace AdivinaQue.Engine;

public sealed class Match
{
    private readonly IReadOnlyList<Card> _deck;
    private readonly IClock _clock;
    private readonly ISeededRandom _random;
    private readonly MatchOptions _options;
    private readonly HashSet<Guid> _processedActionIds = new();
    private readonly Dictionary<Guid, Card> _secretCards = new();
    private readonly Dictionary<Guid, HashSet<string>> _eliminations = new();
    private readonly List<QuestionEntry> _history = new();

    private bool _readyA;
    private bool _readyB;
    private QuestionEntry? _pendingQuestion;
    private Guid? _pausedPlayerId;
    private DateTimeOffset? _pausedAt;

    private Match(Guid playerA, Guid playerB, IReadOnlyList<Card> deck, IClock clock, ISeededRandom random, MatchOptions options)
    {
        PlayerA = playerA;
        PlayerB = playerB;
        _deck = deck;
        _clock = clock;
        _random = random;
        _options = options;
        Status = GameStatus.Lobby;
        _eliminations[playerA] = new HashSet<string>();
        _eliminations[playerB] = new HashSet<string>();
    }

    public Guid PlayerA { get; }

    public Guid PlayerB { get; }

    public GameStatus Status { get; private set; }

    public TurnPhase Phase { get; private set; }

    public Guid ActivePlayerId { get; private set; }

    public long StateVersion { get; private set; }

    public Guid? Winner { get; private set; }

    public FinishReason? Reason { get; private set; }

    public static Match Create(
        Guid playerA,
        Guid playerB,
        IReadOnlyList<Card> deck,
        IClock clock,
        ISeededRandom random,
        MatchOptions? options = null)
    {
        if (playerA == playerB)
        {
            throw new ArgumentException("Los dos jugadores deben ser distintos.", nameof(playerB));
        }

        if (deck is null || deck.Count < 2)
        {
            throw new ArgumentException("El mazo necesita al menos 2 cartas.", nameof(deck));
        }

        if (deck.Select(c => c.Id).Distinct().Count() != deck.Count)
        {
            throw new ArgumentException("El mazo tiene ids de carta duplicados.", nameof(deck));
        }

        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(random);

        return new Match(playerA, playerB, deck, clock, random, options ?? new MatchOptions());
    }

    public Result SetReady(Guid playerId)
    {
        AdvanceTime();

        if (!IsKnownPlayer(playerId))
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        if (Status != GameStatus.Lobby)
        {
            return Result.Fail(ErrorCode.WrongState);
        }

        if (playerId == PlayerA)
        {
            _readyA = true;
        }
        else
        {
            _readyB = true;
        }

        if (_readyA && _readyB)
        {
            Status = GameStatus.Setup;
        }

        StateVersion++;
        return Result.Ok();
    }

    public Result ChooseCharacter(Guid playerId, string cardId)
    {
        AdvanceTime();

        if (!IsKnownPlayer(playerId))
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        if (Status != GameStatus.Setup)
        {
            return Result.Fail(ErrorCode.WrongState);
        }

        var card = _deck.FirstOrDefault(c => c.Id == cardId);
        if (card is null)
        {
            return Result.Fail(ErrorCode.UnknownCard);
        }

        _secretCards[playerId] = card;

        if (_secretCards.ContainsKey(PlayerA) && _secretCards.ContainsKey(PlayerB))
        {
            BeginFirstTurn();
        }

        StateVersion++;
        return Result.Ok();
    }

    public Result AskQuestion(Guid actionId, Guid playerId, string text, SuggestedFrom? suggestedFrom = null)
    {
        AdvanceTime();

        if (_processedActionIds.Contains(actionId))
        {
            return Result.Ok();
        }

        if (!IsKnownPlayer(playerId))
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        if (Status != GameStatus.InTurn)
        {
            return Result.Fail(ErrorCode.WrongState);
        }

        if (Phase != TurnPhase.AwaitingQuestion)
        {
            return Result.Fail(ErrorCode.WrongPhase);
        }

        if (playerId != ActivePlayerId)
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        if (string.IsNullOrWhiteSpace(text) || text.Length > 200)
        {
            return Result.Fail(ErrorCode.TextTooLong);
        }

        var entry = new QuestionEntry(actionId, playerId, text, suggestedFrom, _clock.UtcNow);
        _history.Add(entry);
        _pendingQuestion = entry;
        Phase = TurnPhase.AwaitingAnswer;

        _processedActionIds.Add(actionId);
        StateVersion++;
        return Result.Ok();
    }

    public Result SubmitAnswer(Guid actionId, Guid playerId, Answer answer)
    {
        AdvanceTime();

        if (_processedActionIds.Contains(actionId))
        {
            return Result.Ok();
        }

        if (!IsKnownPlayer(playerId))
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        if (Status != GameStatus.InTurn)
        {
            return Result.Fail(ErrorCode.WrongState);
        }

        if (Phase != TurnPhase.AwaitingAnswer || _pendingQuestion is null)
        {
            return Result.Fail(ErrorCode.WrongPhase);
        }

        var responder = OpponentOf(ActivePlayerId);
        if (playerId != responder)
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        _pendingQuestion.Resolution = answer switch
        {
            Answer.Yes => QuestionResolution.Yes,
            Answer.No => QuestionResolution.No,
            Answer.NotApplicable => QuestionResolution.NotApplicable,
            _ => throw new ArgumentOutOfRangeException(nameof(answer)),
        };
        _pendingQuestion.ResolvedAt = _clock.UtcNow;

        Phase = answer == Answer.NotApplicable
            ? TurnPhase.AwaitingQuestion
            : TurnPhase.AwaitingEliminations;

        _pendingQuestion = null;
        _processedActionIds.Add(actionId);
        StateVersion++;
        return Result.Ok();
    }

    public Result ToggleElimination(Guid playerId, string cardId)
    {
        AdvanceTime();

        if (!IsKnownPlayer(playerId))
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        if (Status != GameStatus.InTurn && Status != GameStatus.Paused)
        {
            return Result.Fail(ErrorCode.WrongState);
        }

        if (_deck.All(c => c.Id != cardId))
        {
            return Result.Fail(ErrorCode.UnknownCard);
        }

        var set = _eliminations[playerId];
        if (!set.Remove(cardId))
        {
            set.Add(cardId);
        }

        StateVersion++;
        return Result.Ok();
    }

    public Result EndTurn(Guid actionId, Guid playerId)
    {
        AdvanceTime();

        if (_processedActionIds.Contains(actionId))
        {
            return Result.Ok();
        }

        if (!IsKnownPlayer(playerId))
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        if (Status != GameStatus.InTurn)
        {
            return Result.Fail(ErrorCode.WrongState);
        }

        if (Phase != TurnPhase.AwaitingEliminations)
        {
            return Result.Fail(ErrorCode.WrongPhase);
        }

        if (playerId != ActivePlayerId)
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        ActivePlayerId = OpponentOf(ActivePlayerId);
        Phase = TurnPhase.AwaitingQuestion;

        _processedActionIds.Add(actionId);
        StateVersion++;
        return Result.Ok();
    }

    public Result MakeGuess(Guid actionId, Guid playerId, string cardId)
    {
        AdvanceTime();

        if (_processedActionIds.Contains(actionId))
        {
            return Result.Ok();
        }

        if (!IsKnownPlayer(playerId))
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        if (Status != GameStatus.InTurn)
        {
            return Result.Fail(ErrorCode.WrongState);
        }

        if (Phase != TurnPhase.AwaitingQuestion)
        {
            return Result.Fail(ErrorCode.WrongPhase);
        }

        if (playerId != ActivePlayerId)
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        if (_deck.All(c => c.Id != cardId))
        {
            return Result.Fail(ErrorCode.UnknownCard);
        }

        var opponent = OpponentOf(playerId);
        var correct = _secretCards[opponent].Id == cardId;
        _processedActionIds.Add(actionId);

        if (correct)
        {
            Status = GameStatus.Finished;
            Winner = playerId;
            Reason = FinishReason.CorrectGuess;
        }
        else if (_options.WrongGuessPolicy == WrongGuessPolicy.EndsMatch)
        {
            Status = GameStatus.Finished;
            Winner = opponent;
            Reason = FinishReason.WrongGuess;
        }
        else
        {
            ActivePlayerId = opponent;
            Phase = TurnPhase.AwaitingQuestion;
        }

        StateVersion++;
        return Result.Ok();
    }

    public Result Disconnect(Guid playerId)
    {
        AdvanceTime();

        if (!IsKnownPlayer(playerId))
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        if (Status != GameStatus.InTurn)
        {
            return Result.Fail(ErrorCode.WrongState);
        }

        _pausedPlayerId = playerId;
        _pausedAt = _clock.UtcNow;
        Status = GameStatus.Paused;

        StateVersion++;
        return Result.Ok();
    }

    public Result Reconnect(Guid playerId)
    {
        AdvanceTime();

        if (!IsKnownPlayer(playerId))
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        if (Status != GameStatus.Paused)
        {
            return Result.Fail(ErrorCode.WrongState);
        }

        if (playerId != _pausedPlayerId)
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        _pausedPlayerId = null;
        _pausedAt = null;
        Status = GameStatus.InTurn;

        StateVersion++;
        return Result.Ok();
    }

    public Result Leave(Guid playerId)
    {
        AdvanceTime();

        if (!IsKnownPlayer(playerId))
        {
            return Result.Fail(ErrorCode.WrongActor);
        }

        switch (Status)
        {
            case GameStatus.Finished:
            case GameStatus.Abandoned:
                return Result.Fail(ErrorCode.WrongState);
            case GameStatus.Lobby:
            case GameStatus.Setup:
                Status = GameStatus.Abandoned;
                break;
            default:
                Status = GameStatus.Finished;
                Winner = OpponentOf(playerId);
                Reason = FinishReason.Forfeit;
                break;
        }

        StateVersion++;
        return Result.Ok();
    }

    public Card GetSecretCard(Guid playerId) => _secretCards[playerId];

    public MatchSnapshot ToSnapshot()
    {
        return new MatchSnapshot(
            PlayerA,
            PlayerB,
            _deck,
            _options,
            Status,
            Phase,
            ActivePlayerId,
            StateVersion,
            Winner,
            Reason,
            _readyA,
            _readyB,
            new Dictionary<Guid, Card>(_secretCards),
            _eliminations.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.ToList()),
            _history.Select(q => new QuestionEntrySnapshot(
                q.ActionId, q.AskedByPlayerId, q.Text, q.SuggestedFrom, q.AskedAt, q.Resolution, q.ResolvedAt)).ToList(),
            _pendingQuestion?.ActionId,
            _pausedPlayerId,
            _pausedAt,
            _processedActionIds.ToList());
    }

    public static Match FromSnapshot(MatchSnapshot snapshot, IClock clock, ISeededRandom random)
    {
        var match = new Match(snapshot.PlayerA, snapshot.PlayerB, snapshot.Deck, clock, random, snapshot.Options)
        {
            Status = snapshot.Status,
            Phase = snapshot.Phase,
            ActivePlayerId = snapshot.ActivePlayerId,
            StateVersion = snapshot.StateVersion,
            Winner = snapshot.Winner,
            Reason = snapshot.Reason,
            _readyA = snapshot.ReadyA,
            _readyB = snapshot.ReadyB,
            _pausedPlayerId = snapshot.PausedPlayerId,
            _pausedAt = snapshot.PausedAt,
        };

        foreach (var (playerId, card) in snapshot.SecretCards)
        {
            match._secretCards[playerId] = card;
        }

        foreach (var (playerId, cardIds) in snapshot.Eliminations)
        {
            match._eliminations[playerId] = new HashSet<string>(cardIds);
        }

        foreach (var entrySnapshot in snapshot.History)
        {
            var entry = new QuestionEntry(
                entrySnapshot.ActionId,
                entrySnapshot.AskedByPlayerId,
                entrySnapshot.Text,
                entrySnapshot.SuggestedFrom,
                entrySnapshot.AskedAt)
            {
                Resolution = entrySnapshot.Resolution,
                ResolvedAt = entrySnapshot.ResolvedAt,
            };

            match._history.Add(entry);
            if (entrySnapshot.ActionId == snapshot.PendingQuestionActionId)
            {
                match._pendingQuestion = entry;
            }
        }

        foreach (var actionId in snapshot.ProcessedActionIds)
        {
            match._processedActionIds.Add(actionId);
        }

        return match;
    }

    public Projection GetProjection(Guid viewerPlayerId)
    {
        AdvanceTime();

        if (!IsKnownPlayer(viewerPlayerId))
        {
            throw new ArgumentException("El jugador no pertenece a esta partida.", nameof(viewerPlayerId));
        }

        _secretCards.TryGetValue(viewerPlayerId, out var yourCard);

        var history = _history
            .Select(q => new QuestionView(q.ActionId, q.AskedByPlayerId, q.Text, q.SuggestedFrom, q.Resolution))
            .ToList();

        FinishInfo? finish = Status == GameStatus.Finished && Winner is not null && Reason is not null
            ? new FinishInfo(Winner.Value, Reason.Value, new Dictionary<Guid, Card>(_secretCards))
            : null;

        PauseInfo? pause = Status == GameStatus.Paused && _pausedPlayerId is not null && _pausedAt is not null
            ? new PauseInfo(_pausedPlayerId.Value, _pausedAt.Value)
            : null;

        var showsTurnState = Status is GameStatus.InTurn or GameStatus.Paused;

        return new Projection(
            Status,
            showsTurnState ? Phase : null,
            showsTurnState ? ActivePlayerId : null,
            StateVersion,
            _deck,
            yourCard,
            _eliminations.TryGetValue(viewerPlayerId, out var set) ? set : Array.Empty<string>(),
            history,
            pause,
            finish);
    }

    public void AdvanceTime()
    {
        if (Status == GameStatus.InTurn && Phase == TurnPhase.AwaitingAnswer && _pendingQuestion is not null)
        {
            var deadline = _pendingQuestion.AskedAt + _options.AnswerTimeout;
            if (_clock.UtcNow >= deadline)
            {
                ExpireQuestion();
            }
        }

        if (Status == GameStatus.Paused && _pausedAt is not null)
        {
            var deadline = _pausedAt.Value + _options.DisconnectGrace;
            if (_clock.UtcNow >= deadline)
            {
                ForfeitPausedPlayer();
            }
        }
    }

    private void ExpireQuestion()
    {
        if (_pendingQuestion is null)
        {
            return;
        }

        _pendingQuestion.Resolution = QuestionResolution.Expired;
        _pendingQuestion.ResolvedAt = _clock.UtcNow;
        _pendingQuestion = null;
        Phase = TurnPhase.AwaitingQuestion;
        StateVersion++;
    }

    private void ForfeitPausedPlayer()
    {
        var pausedPlayerId = _pausedPlayerId!.Value;
        _pausedPlayerId = null;
        _pausedAt = null;
        Status = GameStatus.Finished;
        Winner = OpponentOf(pausedPlayerId);
        Reason = FinishReason.Forfeit;
        StateVersion++;
    }

    private void BeginFirstTurn()
    {
        ActivePlayerId = _random.Next(0, 2) == 0 ? PlayerA : PlayerB;
        Phase = TurnPhase.AwaitingQuestion;
        Status = GameStatus.InTurn;
    }

    private bool IsKnownPlayer(Guid playerId) => playerId == PlayerA || playerId == PlayerB;

    private Guid OpponentOf(Guid playerId) => playerId == PlayerA ? PlayerB : PlayerA;
}
