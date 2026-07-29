using System.Text.Json;
using AdivinaQue.Contracts.ContentPack;
using AdivinaQue.PackTool.Validation;

namespace AdivinaQue.PackTool.Model;

/// <summary>
/// Combina un <see cref="PackDefinition"/> crudo con los valores de cada carta ya
/// resueltos a su tipo real (bool / string / string[]) según el atributo declarado.
/// R8 (completitud/consistencia carta-atributo) se detecta acá, durante la resolución.
/// </summary>
public sealed class ResolvedPack
{
    private ResolvedPack(
        PackDefinition definition,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> cardValues,
        IReadOnlyList<ValidationFinding> issues)
    {
        Definition = definition;
        CardValues = cardValues;
        Issues = issues;
    }

    public PackDefinition Definition { get; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> CardValues { get; }

    public IReadOnlyList<ValidationFinding> Issues { get; }

    public static ResolvedPack Build(PackDefinition definition)
    {
        var issues = new List<ValidationFinding>();
        var cardValues = new Dictionary<string, IReadOnlyDictionary<string, object>>();

        foreach (var card in definition.Cartas)
        {
            var values = new Dictionary<string, object>();

            foreach (var attribute in definition.Atributos)
            {
                if (!card.Atributos.TryGetValue(attribute.Id, out var raw))
                {
                    issues.Add(ValidationFinding.Error(
                        Rule.R8,
                        $"La carta '{card.Id}' no tiene un valor para el atributo '{attribute.Id}'."));
                    continue;
                }

                if (TryResolveValue(card, attribute, raw, issues, out var resolved))
                {
                    values[attribute.Id] = resolved!;
                }
            }

            cardValues[card.Id] = values;
        }

        return new ResolvedPack(definition, cardValues, issues);
    }

    private static bool TryResolveValue(
        CardDefinition card,
        AttributeDefinition attribute,
        JsonElement raw,
        List<ValidationFinding> issues,
        out object? resolved)
    {
        resolved = null;

        switch (attribute.Tipo)
        {
            case AttributeType.Booleano:
                if (raw.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    issues.Add(ValidationFinding.Error(
                        Rule.R8,
                        $"La carta '{card.Id}' tiene un valor no booleano para '{attribute.Id}'."));
                    return false;
                }

                resolved = raw.GetBoolean();
                return true;

            case AttributeType.Categorico:
            case AttributeType.Ordinal:
                if (raw.ValueKind != JsonValueKind.String)
                {
                    issues.Add(ValidationFinding.Error(
                        Rule.R8,
                        $"La carta '{card.Id}' tiene un valor no textual para '{attribute.Id}'."));
                    return false;
                }

                var singleId = raw.GetString()!;
                if (attribute.Valores is null || attribute.Valores.All(v => v.Id != singleId))
                {
                    issues.Add(ValidationFinding.Error(
                        Rule.R8,
                        $"La carta '{card.Id}' referencia el valor desconocido '{singleId}' en '{attribute.Id}'."));
                    return false;
                }

                resolved = singleId;
                return true;

            case AttributeType.Multivalor:
                if (raw.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(ValidationFinding.Error(
                        Rule.R8,
                        $"La carta '{card.Id}' tiene un valor no-arreglo para '{attribute.Id}'."));
                    return false;
                }

                var ids = raw.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
                var unknown = ids.Where(id => attribute.Valores is null || attribute.Valores.All(v => v.Id != id)).ToList();
                if (unknown.Count > 0)
                {
                    issues.Add(ValidationFinding.Error(
                        Rule.R8,
                        $"La carta '{card.Id}' referencia valores desconocidos ({string.Join(", ", unknown)}) en '{attribute.Id}'."));
                    return false;
                }

                resolved = ids;
                return true;

            default:
                return false;
        }
    }
}
