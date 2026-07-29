using AdivinaQue.PackTool.Analysis;
using AdivinaQue.PackTool.Model;
using FluentAssertions;

namespace AdivinaQue.PackTool.Tests.Analysis;

public class HiddenCardAnalyzerTests
{
    [Fact]
    public void CardOnMinoritySideOfEveryQuestion_HasHighEliminationCount()
    {
        var cardIds = new[] { "loner", "b", "c", "d", "e", "f", "g", "h" };

        // Cada pregunta deja a "loner" solo del lado chico (1 de 8) y al resto (7 de 8)
        // del lado grande: las 3 preguntas ayudan a eliminarlo.
        var questions = new[]
        {
            Question("attr1", "v1", cardIds, id => id == "loner"),
            Question("attr2", "v1", cardIds, id => id == "loner"),
            Question("attr3", "v1", cardIds, id => id == "loner"),
        };

        var counts = HiddenCardAnalyzer.CountEliminatingQuestions(questions, cardIds);

        counts["loner"].Should().Be(3);
    }

    [Fact]
    public void CardAlwaysOnMajoritySide_HasLowEliminationCount()
    {
        var cardIds = new[] { "hidden", "b", "c", "d", "e", "f", "g", "h" };

        // "hidden" siempre queda del lado grande (7 de 8) en cada pregunta -> ninguna
        // de estas preguntas cuenta como "eliminating" para esa carta.
        var questions = new[]
        {
            Question("attr1", "v1", cardIds, id => id == "b"),
            Question("attr2", "v1", cardIds, id => id == "c"),
            Question("attr3", "v1", cardIds, id => id == "d"),
        };

        var counts = HiddenCardAnalyzer.CountEliminatingQuestions(questions, cardIds);

        counts["hidden"].Should().Be(0);
    }

    private static CatalogQuestion Question(string attributeId, string valueId, IReadOnlyList<string> cardIds, Func<string, bool> answer) =>
        new(attributeId, valueId, $"¿{attributeId}.{valueId}?", cardIds.ToDictionary(id => id, answer));
}
