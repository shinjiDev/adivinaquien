using AdivinaQue.PackTool.Model;

namespace AdivinaQue.PackTool.Analysis;

public sealed record RedundantPair(CatalogQuestion First, CatalogQuestion Second, double Phi);

/// <summary>
/// R4: coeficiente phi (correlación de Pearson para variables binarias) entre cada par
/// de preguntas del catálogo utilizable.
/// </summary>
public static class RedundancyAnalyzer
{
    public static IReadOnlyList<RedundantPair> FindRedundantPairs(
        IReadOnlyList<CatalogQuestion> catalog,
        IReadOnlyList<string> cardIds,
        double threshold = 0.9)
    {
        var result = new List<RedundantPair>();

        for (var i = 0; i < catalog.Count; i++)
        {
            for (var j = i + 1; j < catalog.Count; j++)
            {
                var phi = ComputePhi(catalog[i], catalog[j], cardIds);
                if (Math.Abs(phi) >= threshold)
                {
                    result.Add(new RedundantPair(catalog[i], catalog[j], phi));
                }
            }
        }

        return result;
    }

    private static double ComputePhi(CatalogQuestion a, CatalogQuestion b, IReadOnlyList<string> cardIds)
    {
        int n11 = 0, n10 = 0, n01 = 0, n00 = 0;

        foreach (var id in cardIds)
        {
            var av = a.AnswerByCardId.TryGetValue(id, out var x) && x;
            var bv = b.AnswerByCardId.TryGetValue(id, out var y) && y;

            if (av && bv)
            {
                n11++;
            }
            else if (av)
            {
                n10++;
            }
            else if (bv)
            {
                n01++;
            }
            else
            {
                n00++;
            }
        }

        var n1X = n11 + n10;
        var n0X = n01 + n00;
        var nX1 = n11 + n01;
        var nX0 = n10 + n00;

        var denominator = Math.Sqrt((double)n1X * n0X * nX1 * nX0);
        if (denominator == 0)
        {
            return 0;
        }

        return ((n11 * n00) - (n10 * n01)) / denominator;
    }
}
