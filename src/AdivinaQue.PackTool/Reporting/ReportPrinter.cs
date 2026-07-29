using AdivinaQue.Contracts.ContentPack;
using AdivinaQue.PackTool.Analysis;
using AdivinaQue.PackTool.Validation;

namespace AdivinaQue.PackTool.Reporting;

public static class ReportPrinter
{
    public static void Print(TextWriter output, PackDefinition pack, PackValidationResult result)
    {
        output.WriteLine($"=== Reporte de validación: {pack.PackId} ({pack.Cartas.Count} cartas) ===");
        output.WriteLine();

        output.WriteLine("-- Distribución y entropía por pregunta --");
        foreach (var question in result.Catalog.OrderBy(q => q.AttributeId).ThenBy(q => q.ValueId))
        {
            var p = question.YesFraction(pack.Cartas.Count);
            var h = Entropy.Compute(p);
            output.WriteLine($"  {question.AttributeId}.{question.ValueId} \"{question.Pregunta}\" -> sí={p:P0}  H={h:F2} bits");
        }

        output.WriteLine();

        output.WriteLine($"-- Matriz de redundancia (|phi| >= 0.9): {result.RedundantPairs.Count} par(es) --");
        foreach (var pair in result.RedundantPairs)
        {
            output.WriteLine($"  {pair.First.AttributeId}.{pair.First.ValueId} <-> {pair.Second.AttributeId}.{pair.Second.ValueId}  phi={pair.Phi:F2}");
        }

        output.WriteLine();

        output.WriteLine("-- Árbol de decisión (heurística greedy, no exhaustiva) --");
        output.WriteLine($"  Peor caso: {result.Tree.WorstCaseDepth} preguntas");
        output.WriteLine($"  Promedio:  {result.Tree.AverageDepth:F2} preguntas");
        output.WriteLine();

        output.WriteLine("-- Cartas por número de preguntas utilizables que las eliminan --");
        foreach (var pair in result.EliminationCounts.OrderBy(kv => kv.Value))
        {
            output.WriteLine($"  {pair.Key}: {pair.Value}");
        }

        output.WriteLine();

        var errorCount = result.Findings.Count(f => f.Severity == Severity.Error);
        var warningCount = result.Findings.Count(f => f.Severity == Severity.Warning);
        output.WriteLine($"-- Hallazgos ({errorCount} error(es), {warningCount} aviso(s)) --");
        foreach (var finding in result.Findings.OrderBy(f => f.Severity).ThenBy(f => f.Rule))
        {
            output.WriteLine($"  [{finding.Severity}] {finding.Rule}: {finding.Message}");
        }

        output.WriteLine();
        output.WriteLine(result.HasErrors ? "RESULTADO: RECHAZADO" : "RESULTADO: ACEPTADO");
    }
}
