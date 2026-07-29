namespace AdivinaQue.PackTool.Model;

/// <summary>
/// Una sugerencia de pregunta derivada de un par (atributo, valor) del pack, con la
/// respuesta sí/no calculada para cada carta. Misma forma que el <c>SuggestedFrom</c>
/// de <c>AdivinaQue.Engine</c> (AttributeId + ValueId), pero es un tipo independiente:
/// Engine no tiene referencias externas y no debe depender de Contracts ni de PackTool.
/// </summary>
public sealed record CatalogQuestion(
    string AttributeId,
    string ValueId,
    string Pregunta,
    IReadOnlyDictionary<string, bool> AnswerByCardId)
{
    public int YesCount => AnswerByCardId.Values.Count(v => v);

    public double YesFraction(int deckSize) => deckSize == 0 ? 0 : (double)YesCount / deckSize;
}
