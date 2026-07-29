using System.Text.Json;
using AdivinaQue.Engine.Tests.TestDoubles;
using FluentAssertions;

namespace AdivinaQue.Engine.Tests;

public class ProjectionSecrecyTests
{
    [Fact]
    public void InTurn_Projection_NeverSerializesOpponentSecretCard()
    {
        var match = MatchFactory.CreateInTurn(new FakeClock(DateTimeOffset.UtcNow));
        AssertOpponentCardNeverLeaks(match, MatchFactory.PlayerA, MatchFactory.PlayerB);
        AssertOpponentCardNeverLeaks(match, MatchFactory.PlayerB, MatchFactory.PlayerA);
    }

    [Fact]
    public void Paused_Projection_NeverSerializesOpponentSecretCard()
    {
        var match = MatchFactory.CreatePaused(new FakeClock(DateTimeOffset.UtcNow), out _);
        AssertOpponentCardNeverLeaks(match, MatchFactory.PlayerA, MatchFactory.PlayerB);
        AssertOpponentCardNeverLeaks(match, MatchFactory.PlayerB, MatchFactory.PlayerA);
    }

    [Fact]
    public void Projection_AlwaysShowsYourOwnCardOnceAssigned()
    {
        var match = MatchFactory.CreateInTurn(new FakeClock(DateTimeOffset.UtcNow));

        var projectionA = match.GetProjection(MatchFactory.PlayerA);
        var projectionB = match.GetProjection(MatchFactory.PlayerB);

        projectionA.YourCard.Should().Be(match.GetSecretCard(MatchFactory.PlayerA));
        projectionB.YourCard.Should().Be(match.GetSecretCard(MatchFactory.PlayerB));
    }

    [Fact]
    public void Finished_Projection_RevealsBothSecretCards()
    {
        var match = MatchFactory.CreateFinished(new FakeClock(DateTimeOffset.UtcNow));

        var projection = match.GetProjection(MatchFactory.PlayerA);

        projection.Finish.Should().NotBeNull();
        projection.Finish!.RevealedCards[MatchFactory.PlayerA].Should().Be(match.GetSecretCard(MatchFactory.PlayerA));
        projection.Finish.RevealedCards[MatchFactory.PlayerB].Should().Be(match.GetSecretCard(MatchFactory.PlayerB));
    }

    private static void AssertOpponentCardNeverLeaks(Match match, Guid viewerId, Guid opponentId)
    {
        var opponentCardId = match.GetSecretCard(opponentId).Id;

        var projection = match.GetProjection(viewerId);
        projection.Finish.Should().BeNull("la partida no ha terminado, no debería haber revelación todavía");
        projection.YourCard!.Id.Should().NotBe(opponentCardId);

        // El mazo completo (Projection.Deck) es información pública — todas las cartas
        // posibles, sin importar a quién se le asignó cada una — así que legítimamente
        // contiene el id de la carta del oponente igual que el de cualquier otra. Lo que
        // nunca debe pasar es que algún OTRO campo la identifique como la secreta del
        // oponente; se serializa sin el mazo para chequear justamente eso.
        var withoutDeck = projection with { Deck = Array.Empty<Card>() };
        var payload = JsonSerializer.Serialize(withoutDeck);
        payload.Should().NotContain(
            opponentCardId,
            "fuera del catálogo público del mazo, la carta secreta del oponente jamás debe aparecer");
    }
}
