using AdivinaQue.PackTool.Model;

namespace AdivinaQue.PackTool.Analysis;

/// <summary>
/// R6: cuenta, por carta, cuántas preguntas del catálogo utilizable la dejan del lado
/// igual-o-más-chico de su partición (mismo criterio minimax que R5) — esas son las
/// preguntas que realmente ayudan a acorralar esa carta específica.
/// </summary>
public static class HiddenCardAnalyzer
{
    public static IReadOnlyDictionary<string, int> CountEliminatingQuestions(
        IReadOnlyList<CatalogQuestion> catalog,
        IReadOnlyList<string> cardIds)
    {
        var counts = cardIds.ToDictionary(id => id, _ => 0);

        foreach (var question in catalog)
        {
            var yesCount = cardIds.Count(id => question.AnswerByCardId.TryGetValue(id, out var a) && a);
            var noCount = cardIds.Count - yesCount;

            foreach (var cardId in cardIds)
            {
                var isYes = question.AnswerByCardId.TryGetValue(cardId, out var a) && a;
                var ownGroupSize = isYes ? yesCount : noCount;
                var otherGroupSize = cardIds.Count - ownGroupSize;

                if (ownGroupSize <= otherGroupSize)
                {
                    counts[cardId]++;
                }
            }
        }

        return counts;
    }
}
