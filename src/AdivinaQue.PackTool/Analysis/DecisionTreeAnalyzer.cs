using AdivinaQue.PackTool.Model;

namespace AdivinaQue.PackTool.Analysis;

/// <summary>
/// R5: profundidad del árbol de decisión sobre el catálogo utilizable. Usa una
/// heurística greedy tipo ID3 (en cada nodo, la pregunta que minimiza el tamaño del
/// grupo más grande resultante — el criterio minimax típico de juegos de adivinanza),
/// no una búsqueda exhaustiva del árbol óptimo real (NP-duro en general y el spec no
/// pide fuerza bruta). Desempate determinista por (AttributeId, ValueId) para que el
/// resultado sea reproducible.
/// </summary>
public static class DecisionTreeAnalyzer
{
    public static DecisionTreeResult Analyze(IReadOnlyList<CatalogQuestion> catalog, IReadOnlyList<string> cardIds)
    {
        var leafDepths = new List<int>();
        Recurse(catalog, cardIds, 0, leafDepths);

        var worst = leafDepths.Count == 0 ? 0 : leafDepths.Max();
        var average = leafDepths.Count == 0 ? 0 : leafDepths.Average();
        return new DecisionTreeResult(worst, average);
    }

    private static void Recurse(IReadOnlyList<CatalogQuestion> catalog, IReadOnlyList<string> cardIds, int depth, List<int> leafDepths)
    {
        if (cardIds.Count <= 1)
        {
            leafDepths.Add(depth);
            return;
        }

        CatalogQuestion? best = null;
        List<string>? bestYes = null;
        List<string>? bestNo = null;
        var bestWorstBranch = int.MaxValue;

        foreach (var question in catalog.OrderBy(q => q.AttributeId).ThenBy(q => q.ValueId))
        {
            var yes = cardIds.Where(id => question.AnswerByCardId.TryGetValue(id, out var a) && a).ToList();
            var no = cardIds.Where(id => !(question.AnswerByCardId.TryGetValue(id, out var a) && a)).ToList();

            if (yes.Count == 0 || no.Count == 0)
            {
                continue;
            }

            var worstBranch = Math.Max(yes.Count, no.Count);
            if (worstBranch < bestWorstBranch)
            {
                bestWorstBranch = worstBranch;
                best = question;
                bestYes = yes;
                bestNo = no;
            }
        }

        if (best is null)
        {
            // Ninguna pregunta del catálogo utilizable separa a este grupo: son
            // indistinguibles con el vocabulario visible del mazo. Se carga como una
            // sola hoja "de emergencia" a una profundidad que garantiza que R5 lo marque
            // como error en vez de subestimar el problema.
            leafDepths.Add(depth + cardIds.Count - 1);
            return;
        }

        Recurse(catalog, bestYes!, depth + 1, leafDepths);
        Recurse(catalog, bestNo!, depth + 1, leafDepths);
    }
}
