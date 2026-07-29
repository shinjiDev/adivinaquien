using AdivinaQue.Contracts.ContentPack;

namespace AdivinaQue.PackTool.Model;

public static class CatalogBuilder
{
    public static IReadOnlyList<CatalogQuestion> Build(ResolvedPack pack)
    {
        var questions = new List<CatalogQuestion>();

        foreach (var attribute in pack.Definition.Atributos)
        {
            switch (attribute.Tipo)
            {
                case AttributeType.Booleano:
                    questions.Add(BuildBooleanQuestion(pack, attribute));
                    break;

                case AttributeType.Categorico:
                case AttributeType.Ordinal:
                    foreach (var value in attribute.Valores ?? Array.Empty<AttributeValueDefinition>())
                    {
                        questions.Add(BuildEqualityQuestion(pack, attribute, value));
                    }

                    break;

                case AttributeType.Multivalor:
                    foreach (var value in attribute.Valores ?? Array.Empty<AttributeValueDefinition>())
                    {
                        questions.Add(BuildMembershipQuestion(pack, attribute, value));
                    }

                    break;
            }
        }

        return questions;
    }

    private static CatalogQuestion BuildBooleanQuestion(ResolvedPack pack, AttributeDefinition attribute)
    {
        var answers = pack.Definition.Cartas.ToDictionary(
            c => c.Id,
            c => pack.CardValues[c.Id].TryGetValue(attribute.Id, out var v) && v is true);

        return new CatalogQuestion(attribute.Id, "true", attribute.Pregunta ?? attribute.Etiqueta, answers);
    }

    private static CatalogQuestion BuildEqualityQuestion(ResolvedPack pack, AttributeDefinition attribute, AttributeValueDefinition value)
    {
        var answers = pack.Definition.Cartas.ToDictionary(
            c => c.Id,
            c => pack.CardValues[c.Id].TryGetValue(attribute.Id, out var v) && v is string s && s == value.Id);

        return new CatalogQuestion(attribute.Id, value.Id, value.Pregunta, answers);
    }

    private static CatalogQuestion BuildMembershipQuestion(ResolvedPack pack, AttributeDefinition attribute, AttributeValueDefinition value)
    {
        var answers = pack.Definition.Cartas.ToDictionary(
            c => c.Id,
            c => pack.CardValues[c.Id].TryGetValue(attribute.Id, out var v) && v is string[] arr && arr.Contains(value.Id));

        return new CatalogQuestion(attribute.Id, value.Id, value.Pregunta, answers);
    }
}
