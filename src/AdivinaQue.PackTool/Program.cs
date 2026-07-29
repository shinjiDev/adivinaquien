using AdivinaQue.Contracts.ContentPack;
using AdivinaQue.PackTool.Model;
using AdivinaQue.PackTool.Reporting;
using AdivinaQue.PackTool.Validation;

namespace AdivinaQue.PackTool;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out);

    public static int Run(string[] args, TextWriter output)
    {
        if (args.Length != 2 || args[0] != "validate")
        {
            output.WriteLine("Uso: packtool validate <ruta-a-pack.json>");
            return 2;
        }

        var path = args[1];
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            output.WriteLine($"No se pudo leer '{path}': {ex.Message}");
            return 2;
        }

        PackDefinition definition;
        try
        {
            definition = PackParser.Parse(json);
        }
        catch (Exception ex)
        {
            output.WriteLine($"El pack no es JSON válido: {ex.Message}");
            return 2;
        }

        var resolved = ResolvedPack.Build(definition);
        var result = PackValidator.Validate(resolved);

        ReportPrinter.Print(output, definition, result);

        return result.HasErrors ? 1 : 0;
    }
}
