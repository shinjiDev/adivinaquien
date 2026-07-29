using System.Text.Json;
using AdivinaQue.Contracts.ContentPack;
using FluentAssertions;

namespace AdivinaQue.PackTool.Tests;

public class PackParserTests
{
    private const string ExampleFromSkill = """
    {
      "packId": "bailes-chile",
      "nombre": "Bailes típicos de Chile",
      "descripcion": "Adivina el baile folclórico chileno oculto del oponente.",
      "idioma": "es-CL",
      "version": "1.0.0",
      "atributos": [
        {
          "id": "zona",
          "tipo": "categorico",
          "etiqueta": "Zona",
          "valores": [
            { "id": "norte",    "etiqueta": "Norte",    "pregunta": "¿Es un baile de la zona norte?" },
            { "id": "centro",   "etiqueta": "Centro",   "pregunta": "¿Es un baile de la zona central?" },
            { "id": "sur",      "etiqueta": "Sur",      "pregunta": "¿Es un baile de la zona sur?" },
            { "id": "austral",  "etiqueta": "Austral",  "pregunta": "¿Es un baile de la zona austral?" },
            { "id": "insular",  "etiqueta": "Insular",  "pregunta": "¿Es un baile de Rapa Nui?" }
          ]
        },
        {
          "id": "usa_panuelo",
          "tipo": "booleano",
          "etiqueta": "Pañuelo",
          "pregunta": "¿Se baila con pañuelo?"
        },
        {
          "id": "instrumentos",
          "tipo": "multivalor",
          "etiqueta": "Instrumentos",
          "valores": [
            { "id": "guitarra",  "etiqueta": "Guitarra",  "pregunta": "¿Se acompaña con guitarra?" },
            { "id": "acordeon",  "etiqueta": "Acordeón",  "pregunta": "¿Se acompaña con acordeón?" },
            { "id": "percusion", "etiqueta": "Percusión", "pregunta": "¿Se acompaña con percusión?" }
          ]
        },
        {
          "id": "n_bailarines",
          "tipo": "ordinal",
          "etiqueta": "Formación",
          "valores": [
            { "id": "individual", "orden": 1, "etiqueta": "Individual", "pregunta": "¿Se baila solo?" },
            { "id": "pareja",     "orden": 2, "etiqueta": "En pareja",  "pregunta": "¿Se baila en pareja?" },
            { "id": "grupo",      "orden": 3, "etiqueta": "En grupo",   "pregunta": "¿Se baila en grupo?" }
          ]
        }
      ],
      "cartas": [
        {
          "id": "cueca",
          "nombre": "Cueca",
          "imagen": "img/cueca.webp",
          "atributos": {
            "zona": "centro",
            "usa_panuelo": true,
            "instrumentos": ["guitarra", "acordeon"],
            "n_bailarines": "pareja"
          },
          "ficha": "Texto breve que se muestra al revelar la carta.",
          "fuente": "URL o referencia bibliográfica verificable"
        }
      ]
    }
    """;

    [Fact]
    public void Parse_ReadsTopLevelPackFields()
    {
        var pack = PackParser.Parse(ExampleFromSkill);

        pack.PackId.Should().Be("bailes-chile");
        pack.Idioma.Should().Be("es-CL");
        pack.Atributos.Should().HaveCount(4);
        pack.Cartas.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_Categorico_ReadsValuesInOrder()
    {
        var pack = PackParser.Parse(ExampleFromSkill);
        var zona = pack.Atributos.Single(a => a.Id == "zona");

        zona.Tipo.Should().Be(AttributeType.Categorico);
        zona.Valores.Should().HaveCount(5);
        zona.Valores!.Select(v => v.Id).Should().Equal("norte", "centro", "sur", "austral", "insular");
    }

    [Fact]
    public void Parse_Booleano_HasPreguntaButNoValores()
    {
        var pack = PackParser.Parse(ExampleFromSkill);
        var panuelo = pack.Atributos.Single(a => a.Id == "usa_panuelo");

        panuelo.Tipo.Should().Be(AttributeType.Booleano);
        panuelo.Pregunta.Should().Be("¿Se baila con pañuelo?");
        panuelo.Valores.Should().BeNull();
    }

    [Fact]
    public void Parse_Multivalor_ReadsAllValues()
    {
        var pack = PackParser.Parse(ExampleFromSkill);
        var instrumentos = pack.Atributos.Single(a => a.Id == "instrumentos");

        instrumentos.Tipo.Should().Be(AttributeType.Multivalor);
        instrumentos.Valores!.Select(v => v.Id).Should().BeEquivalentTo("guitarra", "acordeon", "percusion");
    }

    [Fact]
    public void Parse_Ordinal_ReadsOrdenPerValue()
    {
        var pack = PackParser.Parse(ExampleFromSkill);
        var formacion = pack.Atributos.Single(a => a.Id == "n_bailarines");

        formacion.Tipo.Should().Be(AttributeType.Ordinal);
        formacion.Valores!.Select(v => (v.Id, v.Orden)).Should().Equal(
            ("individual", 1),
            ("pareja", 2),
            ("grupo", 3));
    }

    [Fact]
    public void Parse_CardAttributes_KeepRawJsonShapePerType()
    {
        var pack = PackParser.Parse(ExampleFromSkill);
        var cueca = pack.Cartas.Single();

        cueca.Atributos["zona"].GetString().Should().Be("centro");
        cueca.Atributos["usa_panuelo"].ValueKind.Should().Be(JsonValueKind.True);
        cueca.Atributos["instrumentos"].EnumerateArray().Select(e => e.GetString()).Should().Equal("guitarra", "acordeon");
        cueca.Atributos["n_bailarines"].GetString().Should().Be("pareja");
    }

    [Fact]
    public void SerializeThenParseThenSerialize_IsIdempotent()
    {
        var original = PackJsonFixtures.BuildValidPack();

        var json = PackParser.Serialize(original);
        var reparsed = PackParser.Parse(json);
        var reserialized = PackParser.Serialize(reparsed);

        reserialized.Should().Be(json);
    }
}
