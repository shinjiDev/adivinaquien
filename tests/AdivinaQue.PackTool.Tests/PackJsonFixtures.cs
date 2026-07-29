using System.Text.Json;
using AdivinaQue.Contracts.ContentPack;

namespace AdivinaQue.PackTool.Tests;

/// <summary>
/// Construye un pack sintético de 16 cartas, 6 ejes de atributos y 15 preguntas de
/// catálogo, todas dentro del rango ideal 25%-75%, y con vector único garantizado por
/// construcción (zona + usa_panuelo + usa_mascara son, juntos, una biyección sobre el
/// índice de la carta). Las pruebas parten de esto y lo rompen a propósito.
/// </summary>
public static class PackJsonFixtures
{
    public static PackDefinition BuildValidPack()
    {
        var zona = new AttributeDefinition(
            "zona",
            AttributeType.Categorico,
            "Zona",
            Valores: new[]
            {
                new AttributeValueDefinition("norte", "Norte", "¿Es un baile de la zona norte?"),
                new AttributeValueDefinition("centro", "Centro", "¿Es un baile de la zona central?"),
                new AttributeValueDefinition("sur", "Sur", "¿Es un baile de la zona sur?"),
                new AttributeValueDefinition("austral", "Austral", "¿Es un baile de la zona austral?"),
            });

        var usaPanuelo = new AttributeDefinition("usa_panuelo", AttributeType.Booleano, "Pañuelo", Pregunta: "¿Se baila con pañuelo?");
        var usaMascara = new AttributeDefinition("usa_mascara", AttributeType.Booleano, "Máscara", Pregunta: "¿Se usa máscara?");

        var instrumentos = new AttributeDefinition(
            "instrumentos",
            AttributeType.Multivalor,
            "Instrumentos",
            Valores: new[]
            {
                new AttributeValueDefinition("guitarra", "Guitarra", "¿Se acompaña con guitarra?"),
                new AttributeValueDefinition("acordeon", "Acordeón", "¿Se acompaña con acordeón?"),
                new AttributeValueDefinition("percusion", "Percusión", "¿Se acompaña con percusión?"),
            });

        var nBailarines = new AttributeDefinition(
            "n_bailarines",
            AttributeType.Ordinal,
            "Formación",
            Valores: new[]
            {
                new AttributeValueDefinition("individual", "Individual", "¿Se baila solo?", Orden: 1),
                new AttributeValueDefinition("pareja", "En pareja", "¿Se baila en pareja?", Orden: 2),
                new AttributeValueDefinition("grupo", "En grupo", "¿Se baila en grupo?", Orden: 3),
            });

        var caracter = new AttributeDefinition(
            "caracter",
            AttributeType.Categorico,
            "Carácter",
            Valores: new[]
            {
                new AttributeValueDefinition("festivo", "Festivo", "¿Tiene carácter festivo?"),
                new AttributeValueDefinition("ceremonial", "Ceremonial", "¿Tiene carácter ceremonial?"),
                new AttributeValueDefinition("cortejo", "De cortejo", "¿Es un baile de cortejo?"),
            });

        var attributes = new List<AttributeDefinition> { zona, usaPanuelo, usaMascara, instrumentos, nBailarines, caracter };

        var zonaValues = new[] { "norte", "centro", "sur", "austral" };
        var formacionValues = new[]
        {
            "individual", "individual", "individual", "individual", "individual",
            "pareja", "pareja", "pareja", "pareja", "pareja", "pareja",
            "grupo", "grupo", "grupo", "grupo", "grupo",
        };
        var caracterValues = new[]
        {
            "festivo", "festivo", "festivo", "festivo", "festivo", "festivo",
            "ceremonial", "ceremonial", "ceremonial", "ceremonial", "ceremonial",
            "cortejo", "cortejo", "cortejo", "cortejo", "cortejo",
        };

        var cards = new List<CardDefinition>();
        for (var i = 0; i < 16; i++)
        {
            var zonaValue = zonaValues[i / 4];
            var panuelo = (i & 0b0010) != 0;
            var mascara = (i & 0b0001) != 0;

            var instrumentosValue = new List<string>();
            if (i is >= 0 and <= 7)
            {
                instrumentosValue.Add("guitarra");
            }

            if (i is >= 4 and <= 11)
            {
                instrumentosValue.Add("acordeon");
            }

            if (i is >= 8 and <= 15)
            {
                instrumentosValue.Add("percusion");
            }

            var atributos = new Dictionary<string, JsonElement>
            {
                ["zona"] = JsonSerializer.SerializeToElement(zonaValue),
                ["usa_panuelo"] = JsonSerializer.SerializeToElement(panuelo),
                ["usa_mascara"] = JsonSerializer.SerializeToElement(mascara),
                ["instrumentos"] = JsonSerializer.SerializeToElement(instrumentosValue),
                ["n_bailarines"] = JsonSerializer.SerializeToElement(formacionValues[i]),
                ["caracter"] = JsonSerializer.SerializeToElement(caracterValues[i]),
            };

            cards.Add(new CardDefinition(
                Id: $"card-{i}",
                Nombre: $"Carta {i}",
                Imagen: $"img/card-{i}.webp",
                Atributos: atributos,
                Ficha: "Ficha de prueba.",
                Fuente: "https://example.test/fuente"));
        }

        return new PackDefinition(
            PackId: "test-pack",
            Nombre: "Pack de prueba",
            Descripcion: "Mazo sintético para tests del validador.",
            Idioma: "es-CL",
            Version: "1.0.0",
            Atributos: attributes,
            Cartas: cards);
    }
}
