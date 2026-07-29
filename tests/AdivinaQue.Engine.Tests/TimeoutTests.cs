using AdivinaQue.Engine.Tests.TestDoubles;
using FluentAssertions;

namespace AdivinaQue.Engine.Tests;

public class TimeoutTests
{
    [Fact]
    public void AwaitingAnswer_JustBeforeSixtySeconds_DoesNotExpire()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = MatchFactory.CreateAwaitingAnswer(clock, out _);

        clock.Advance(TimeSpan.FromSeconds(59));
        match.AdvanceTime();

        match.Phase.Should().Be(TurnPhase.AwaitingAnswer);
    }

    [Fact]
    public void AwaitingAnswer_AfterSixtySeconds_ExpiresWithoutConsumingTurn()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = MatchFactory.CreateAwaitingAnswer(clock, out var questionActionId);
        var questioner = match.ActivePlayerId;

        clock.Advance(TimeSpan.FromSeconds(60));
        match.AdvanceTime();

        match.Status.Should().Be(GameStatus.InTurn);
        match.Phase.Should().Be(TurnPhase.AwaitingQuestion);
        match.ActivePlayerId.Should().Be(questioner);

        var projection = match.GetProjection(questioner);
        var expiredEntry = projection.History.Single(h => h.ActionId == questionActionId);
        expiredEntry.Resolution.Should().Be(QuestionResolution.Expired);
    }

    [Fact]
    public void AwaitingAnswer_Expiry_IsDetectedLazilyOnNextAction_NotOnlyViaExplicitAdvanceTime()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = MatchFactory.CreateAwaitingAnswer(clock, out _);
        var questioner = match.ActivePlayerId;
        var responder = MatchFactory.Responder(match);

        clock.Advance(TimeSpan.FromSeconds(61));

        // El respondedor intenta contestar tarde: la pregunta ya expiró, así que la
        // respuesta llega a una fase que ya cambió y se rechaza como fuera de fase.
        var result = match.SubmitAnswer(Guid.NewGuid(), responder, Answer.Yes);

        result.Error.Should().Be(ErrorCode.WrongPhase);
        match.Phase.Should().Be(TurnPhase.AwaitingQuestion);
        match.ActivePlayerId.Should().Be(questioner);
    }

    [Fact]
    public void Paused_JustBeforeGracePeriod_StaysPaused()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = MatchFactory.CreatePaused(clock, out _);

        clock.Advance(TimeSpan.FromSeconds(119));
        match.AdvanceTime();

        match.Status.Should().Be(GameStatus.Paused);
    }

    [Fact]
    public void Paused_AfterGracePeriod_ForfeitsToOpponent()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = MatchFactory.CreatePaused(clock, out var pausedPlayerId);
        var opponent = pausedPlayerId == MatchFactory.PlayerA ? MatchFactory.PlayerB : MatchFactory.PlayerA;

        clock.Advance(TimeSpan.FromSeconds(120));
        match.AdvanceTime();

        match.Status.Should().Be(GameStatus.Finished);
        match.Reason.Should().Be(FinishReason.Forfeit);
        match.Winner.Should().Be(opponent);
    }

    [Fact]
    public void Reconnect_BeforeGracePeriodExpires_ResumesExactSubState()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = MatchFactory.CreateAwaitingEliminations(clock);
        var questioner = match.ActivePlayerId;

        match.Disconnect(questioner).IsSuccess.Should().BeTrue();
        match.Status.Should().Be(GameStatus.Paused);

        clock.Advance(TimeSpan.FromSeconds(30));
        match.Reconnect(questioner).IsSuccess.Should().BeTrue();

        match.Status.Should().Be(GameStatus.InTurn);
        match.Phase.Should().Be(TurnPhase.AwaitingEliminations);
        match.ActivePlayerId.Should().Be(questioner);
    }
}
