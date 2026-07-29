using AdivinaQue.PackTool.Analysis;
using AdivinaQue.PackTool.Model;

namespace AdivinaQue.PackTool.Validation;

public static class PackValidator
{
    public static PackValidationResult Validate(ResolvedPack pack)
    {
        var findings = new List<ValidationFinding>(pack.Issues);

        var deckSize = pack.Definition.Cartas.Count;
        var cardIds = pack.Definition.Cartas.Select(c => c.Id).ToList();

        ValidateR1DeckSize(deckSize, findings);
        ValidateR2UniqueVectors(pack, findings);

        var catalog = CatalogBuilder.Build(pack);
        var usable = ValidateR3AndFilterUsable(catalog, deckSize, findings);
        ValidateR7MinimumUsable(usable, findings);

        var redundantPairs = RedundancyAnalyzer.FindRedundantPairs(usable, cardIds);
        ValidateR4Redundancy(redundantPairs, findings);

        var tree = DecisionTreeAnalyzer.Analyze(usable, cardIds);
        ValidateR5TreeDepth(tree, findings);

        var eliminationCounts = HiddenCardAnalyzer.CountEliminatingQuestions(usable, cardIds);
        ValidateR6HiddenCards(eliminationCounts, findings);

        return new PackValidationResult(findings, catalog, usable, tree, eliminationCounts, redundantPairs);
    }

    private static void ValidateR1DeckSize(int deckSize, List<ValidationFinding> findings)
    {
        if (deckSize < 16 || deckSize > 36)
        {
            findings.Add(ValidationFinding.Error(
                Rule.R1,
                $"El mazo tiene {deckSize} cartas; debe estar entre 16 y 36 (recomendado 24)."));
        }
    }

    private static void ValidateR2UniqueVectors(ResolvedPack pack, List<ValidationFinding> findings)
    {
        var duplicateGroups = pack.Definition.Cartas
            .GroupBy(c => VectorKey(pack, c.Id))
            .Where(g => g.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            var ids = string.Join(", ", group.Select(c => c.Id));
            findings.Add(ValidationFinding.Error(
                Rule.R2,
                $"Las cartas [{ids}] tienen vectores de atributos idénticos; la partida sería irresoluble entre ellas."));
        }
    }

    private static List<CatalogQuestion> ValidateR3AndFilterUsable(
        IReadOnlyList<CatalogQuestion> catalog,
        int deckSize,
        List<ValidationFinding> findings)
    {
        var usable = new List<CatalogQuestion>();

        foreach (var question in catalog)
        {
            var p = question.YesFraction(deckSize);

            if (p < 0.15 || p > 0.85)
            {
                findings.Add(ValidationFinding.Error(
                    Rule.R3,
                    $"La pregunta '{question.Pregunta}' responde sí en {p:P0} del mazo (fuera de 15%-85%)."));
                continue;
            }

            if (p < 0.25 || p > 0.75)
            {
                findings.Add(ValidationFinding.Warning(
                    Rule.R3,
                    $"La pregunta '{question.Pregunta}' responde sí en {p:P0} del mazo (fuera del ideal 25%-75%)."));
            }

            usable.Add(question);
        }

        return usable;
    }

    private static void ValidateR7MinimumUsable(List<CatalogQuestion> usable, List<ValidationFinding> findings)
    {
        if (usable.Count < 12)
        {
            findings.Add(ValidationFinding.Error(
                Rule.R7,
                $"Solo hay {usable.Count} sugerencias utilizables tras aplicar R3; se necesitan al menos 12."));
        }
    }

    private static void ValidateR4Redundancy(IReadOnlyList<RedundantPair> redundantPairs, List<ValidationFinding> findings)
    {
        foreach (var pair in redundantPairs)
        {
            findings.Add(ValidationFinding.Warning(
                Rule.R4,
                $"Las preguntas '{pair.First.Pregunta}' y '{pair.Second.Pregunta}' están correlacionadas (phi={pair.Phi:F2}); son redundantes."));
        }
    }

    private static void ValidateR5TreeDepth(DecisionTreeResult tree, List<ValidationFinding> findings)
    {
        if (tree.WorstCaseDepth > 8)
        {
            findings.Add(ValidationFinding.Error(
                Rule.R5,
                $"El árbol de decisión necesita hasta {tree.WorstCaseDepth} preguntas en el peor caso (máximo aceptado: 8)."));
        }
    }

    private static void ValidateR6HiddenCards(IReadOnlyDictionary<string, int> eliminationCounts, List<ValidationFinding> findings)
    {
        foreach (var (cardId, count) in eliminationCounts.Where(kv => kv.Value < 3))
        {
            findings.Add(ValidationFinding.Warning(
                Rule.R6,
                $"La carta '{cardId}' solo puede eliminarse con {count} pregunta(s) utilizable(s) (mínimo recomendado: 3)."));
        }
    }

    private static string VectorKey(ResolvedPack pack, string cardId)
    {
        var values = pack.CardValues[cardId];
        var parts = pack.Definition.Atributos.Select(a =>
        {
            if (!values.TryGetValue(a.Id, out var v))
            {
                return $"{a.Id}=<falta>";
            }

            return v switch
            {
                bool b => $"{a.Id}={b}",
                string s => $"{a.Id}={s}",
                string[] arr => $"{a.Id}=[{string.Join(",", arr.OrderBy(x => x))}]",
                _ => $"{a.Id}=?",
            };
        });

        return string.Join("|", parts);
    }
}
