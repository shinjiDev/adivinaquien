using AdivinaQue.PackTool.Analysis;
using AdivinaQue.PackTool.Model;
using FluentAssertions;

namespace AdivinaQue.PackTool.Tests.Analysis;

public class DecisionTreeAnalyzerTests
{
    [Fact]
    public void ThreeIndependentBits_ProduceBalancedTreeOfDepthThree()
    {
        var cardIds = Enumerable.Range(0, 8).Select(i => $"c{i}").ToList();
        var bit0 = Question("bit", "0", cardIds, id => (int.Parse(id[1..]) & 1) != 0);
        var bit1 = Question("bit", "1", cardIds, id => (int.Parse(id[1..]) & 2) != 0);
        var bit2 = Question("bit", "2", cardIds, id => (int.Parse(id[1..]) & 4) != 0);

        var result = DecisionTreeAnalyzer.Analyze(new[] { bit0, bit1, bit2 }, cardIds);

        result.WorstCaseDepth.Should().Be(3);
        result.AverageDepth.Should().Be(3);
    }

    [Fact]
    public void ChainOfSingleCardQuestions_ProducesWorstCaseDepthAboveEight()
    {
        // Un catálogo "malo": cada pregunta solo aísla una carta específica del resto,
        // sin ningún eje que parta el mazo de forma balanceada. El árbol greedy termina
        // pelando una carta a la vez -> profundidad lineal, no logarítmica.
        var cardIds = Enumerable.Range(0, 10).Select(i => $"c{i}").ToList();
        var questions = Enumerable.Range(0, 9)
            .Select(i => Question($"attr{i:D2}", "v1", cardIds, id => id == $"c{i}"))
            .ToList();

        var result = DecisionTreeAnalyzer.Analyze(questions, cardIds);

        result.WorstCaseDepth.Should().BeGreaterThan(8);
    }

    private static CatalogQuestion Question(string attributeId, string valueId, IReadOnlyList<string> cardIds, Func<string, bool> answer) =>
        new(attributeId, valueId, $"¿{attributeId}.{valueId}?", cardIds.ToDictionary(id => id, answer));
}
