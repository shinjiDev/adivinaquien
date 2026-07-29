using AdivinaQue.PackTool.Analysis;
using AdivinaQue.PackTool.Model;
using FluentAssertions;

namespace AdivinaQue.PackTool.Tests.Analysis;

public class RedundancyAnalyzerTests
{
    [Fact]
    public void IdenticalQuestions_AreFlaggedAsRedundant()
    {
        var cardIds = new[] { "a", "b", "c", "d" };
        var q1 = Question("attr1", "v1", cardIds, id => id is "a" or "b");
        var q2 = Question("attr2", "v1", cardIds, id => id is "a" or "b"); // idéntica partición -> phi = 1.0

        var pairs = RedundancyAnalyzer.FindRedundantPairs(new[] { q1, q2 }, cardIds);

        pairs.Should().ContainSingle();
        pairs[0].Phi.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void OppositeQuestions_AreAlsoFlaggedAsRedundant()
    {
        var cardIds = new[] { "a", "b", "c", "d" };
        var q1 = Question("attr1", "v1", cardIds, id => id is "a" or "b");
        var q2 = Question("attr2", "v1", cardIds, id => id is "c" or "d"); // partición exactamente invertida -> phi = -1.0

        var pairs = RedundancyAnalyzer.FindRedundantPairs(new[] { q1, q2 }, cardIds);

        pairs.Should().ContainSingle();
        pairs[0].Phi.Should().BeApproximately(-1.0, 0.0001);
    }

    [Fact]
    public void IndependentQuestions_AreNotFlagged()
    {
        var cardIds = new[] { "a", "b", "c", "d" };
        var q1 = Question("attr1", "v1", cardIds, id => id is "a" or "b");
        var q2 = Question("attr2", "v1", cardIds, id => id is "a" or "c");

        var pairs = RedundancyAnalyzer.FindRedundantPairs(new[] { q1, q2 }, cardIds);

        pairs.Should().BeEmpty();
    }

    private static CatalogQuestion Question(string attributeId, string valueId, IReadOnlyList<string> cardIds, Func<string, bool> answer) =>
        new(attributeId, valueId, $"¿{attributeId}.{valueId}?", cardIds.ToDictionary(id => id, answer));
}
