using System.Text.Json;
using AdivinaQue.Contracts.ContentPack;
using AdivinaQue.PackTool.Model;
using AdivinaQue.PackTool.Validation;
using FluentAssertions;

namespace AdivinaQue.PackTool.Tests;

public class PackValidatorTests
{
    [Fact]
    public void ValidBaselinePack_HasNoErrors()
    {
        var result = Validate(PackJsonFixtures.BuildValidPack());

        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void R1_DeckSmallerThan16_IsRejected()
    {
        var pack = PackJsonFixtures.BuildValidPack();
        var tooSmall = pack with { Cartas = pack.Cartas.Take(10).ToList() };

        var result = Validate(tooSmall);

        result.Findings.Should().Contain(f => f.Rule == Rule.R1 && f.Severity == Severity.Error);
    }

    [Fact]
    public void R2_TwoCardsWithIdenticalVector_AreRejected()
    {
        // Criterio de aceptación explícito del spec: "el CLI rechaza un pack con dos
        // cartas de vector idéntico".
        var pack = PackJsonFixtures.BuildValidPack();
        var cards = pack.Cartas.ToList();
        cards[1] = cards[1] with { Atributos = cards[0].Atributos };
        var withDuplicate = pack with { Cartas = cards };

        var result = Validate(withDuplicate);

        result.Findings.Should().Contain(f =>
            f.Rule == Rule.R2 &&
            f.Severity == Severity.Error &&
            f.Message.Contains(cards[0].Id) &&
            f.Message.Contains(cards[1].Id));
    }

    [Fact]
    public void R3_QuestionOutsideFifteenToEightyFivePercent_IsRejected()
    {
        // Criterio de aceptación explícito del spec: "rechaza uno donde alguna pregunta
        // cae fuera del 15%-85%".
        var pack = PackJsonFixtures.BuildValidPack();
        var skewed = pack with
        {
            Cartas = pack.Cartas.Select(c => WithAttribute(c, "usa_mascara", JsonSerializer.SerializeToElement(true))).ToList(),
        };

        var result = Validate(skewed);

        result.Findings.Should().Contain(f =>
            f.Rule == Rule.R3 &&
            f.Severity == Severity.Error &&
            f.Message.Contains("máscara"));
    }

    [Fact]
    public void R7_FewerThanTwelveUsableSuggestions_IsRejected()
    {
        var pack = PackJsonFixtures.BuildValidPack();

        // Colapsar "zona" a un único valor constante deja las 4 preguntas de zona fuera
        // de rango (una en 100%, las otras tres en 0%), bajando el catálogo utilizable
        // de 15 a 11 preguntas.
        var collapsed = pack with
        {
            Cartas = pack.Cartas.Select(c => WithAttribute(c, "zona", JsonSerializer.SerializeToElement("norte"))).ToList(),
        };

        var result = Validate(collapsed);

        result.Findings.Should().Contain(f => f.Rule == Rule.R7 && f.Severity == Severity.Error);
    }

    [Fact]
    public void R8_CardMissingAnAttribute_IsRejected()
    {
        var pack = PackJsonFixtures.BuildValidPack();
        var cards = pack.Cartas.ToList();
        cards[0] = WithoutAttribute(cards[0], "usa_panuelo");
        var incomplete = pack with { Cartas = cards };

        var result = Validate(incomplete);

        result.Findings.Should().Contain(f =>
            f.Rule == Rule.R8 &&
            f.Severity == Severity.Error &&
            f.Message.Contains(cards[0].Id) &&
            f.Message.Contains("usa_panuelo"));
    }

    private static PackValidationResult Validate(PackDefinition pack) => PackValidator.Validate(ResolvedPack.Build(pack));

    private static CardDefinition WithAttribute(CardDefinition card, string attributeId, JsonElement value)
    {
        var dict = new Dictionary<string, JsonElement>(card.Atributos) { [attributeId] = value };
        return card with { Atributos = dict };
    }

    private static CardDefinition WithoutAttribute(CardDefinition card, string attributeId)
    {
        var dict = new Dictionary<string, JsonElement>(card.Atributos);
        dict.Remove(attributeId);
        return card with { Atributos = dict };
    }
}
