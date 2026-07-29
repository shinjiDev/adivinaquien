using AdivinaQue.Engine.Abstractions;
using AdivinaQue.Engine.Tests.TestDoubles;
using FluentAssertions;

namespace AdivinaQue.Engine.Tests;

public class MatchSnapshotTests
{
    [Fact]
    public void RoundTrip_PreservesProjectionForBothPlayers()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = MatchFactory.CreateAwaitingEliminations(clock);
        match.ToggleElimination(match.ActivePlayerId, MatchFactory.BuildDeck().First(c => c.Id != match.GetSecretCard(MatchFactory.Responder(match)).Id).Id);

        var snapshot = match.ToSnapshot();
        var restored = Match.FromSnapshot(snapshot, clock, new SeededRandom(1));

        restored.GetProjection(MatchFactory.PlayerA).Should().BeEquivalentTo(match.GetProjection(MatchFactory.PlayerA));
        restored.GetProjection(MatchFactory.PlayerB).Should().BeEquivalentTo(match.GetProjection(MatchFactory.PlayerB));
        restored.GetSecretCard(MatchFactory.PlayerA).Should().Be(match.GetSecretCard(MatchFactory.PlayerA));
        restored.GetSecretCard(MatchFactory.PlayerB).Should().Be(match.GetSecretCard(MatchFactory.PlayerB));
    }

    [Fact]
    public void RoundTrip_PreservesPendingQuestionAndResumesTimeoutCorrectly()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = MatchFactory.CreateAwaitingAnswer(clock, out var actionId);

        var snapshot = match.ToSnapshot();
        var restored = Match.FromSnapshot(snapshot, clock, new SeededRandom(1));

        clock.Advance(TimeSpan.FromSeconds(60));
        restored.AdvanceTime();

        restored.Phase.Should().Be(TurnPhase.AwaitingQuestion);
        var expiredEntry = restored.GetProjection(restored.ActivePlayerId).History.Single(h => h.ActionId == actionId);
        expiredEntry.Resolution.Should().Be(QuestionResolution.Expired);
    }

    [Fact]
    public void RoundTrip_PreservesIdempotencyOfProcessedActions()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var match = MatchFactory.CreateInTurn(clock);
        var actionId = Guid.NewGuid();
        match.AskQuestion(actionId, match.ActivePlayerId, "¿Primera?");

        var restored = Match.FromSnapshot(match.ToSnapshot(), clock, new SeededRandom(1));
        var versionBefore = restored.StateVersion;

        var replay = restored.AskQuestion(actionId, restored.ActivePlayerId, "¿Otra?");

        replay.IsSuccess.Should().BeTrue();
        restored.StateVersion.Should().Be(versionBefore);
    }
}
