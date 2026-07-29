using AdivinaQue.PackTool.Analysis;
using AdivinaQue.PackTool.Model;

namespace AdivinaQue.PackTool.Validation;

public sealed record PackValidationResult(
    IReadOnlyList<ValidationFinding> Findings,
    IReadOnlyList<CatalogQuestion> Catalog,
    IReadOnlyList<CatalogQuestion> UsableCatalog,
    DecisionTreeResult Tree,
    IReadOnlyDictionary<string, int> EliminationCounts,
    IReadOnlyList<RedundantPair> RedundantPairs)
{
    public bool HasErrors => Findings.Any(f => f.Severity == Severity.Error);
}
