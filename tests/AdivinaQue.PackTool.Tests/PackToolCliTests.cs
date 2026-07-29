using AdivinaQue.Contracts.ContentPack;
using FluentAssertions;

namespace AdivinaQue.PackTool.Tests;

public class PackToolCliTests
{
    [Fact]
    public void Validate_ValidPack_ReturnsZeroAndPrintsAccepted()
    {
        var path = WriteTempPack(PackJsonFixtures.BuildValidPack());
        try
        {
            var writer = new StringWriter();
            var exitCode = Program.Run(new[] { "validate", path }, writer);

            exitCode.Should().Be(0);
            writer.ToString().Should().Contain("RESULTADO: ACEPTADO");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Validate_InvalidPack_ReturnsOneAndPrintsRejected()
    {
        var pack = PackJsonFixtures.BuildValidPack();
        var tooSmall = pack with { Cartas = pack.Cartas.Take(10).ToList() };
        var path = WriteTempPack(tooSmall);
        try
        {
            var writer = new StringWriter();
            var exitCode = Program.Run(new[] { "validate", path }, writer);

            exitCode.Should().Be(1);
            writer.ToString().Should().Contain("RESULTADO: RECHAZADO");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_WithBadArguments_ReturnsTwoAndPrintsUsage()
    {
        var writer = new StringWriter();

        var exitCode = Program.Run(new[] { "not-a-command" }, writer);

        exitCode.Should().Be(2);
        writer.ToString().Should().Contain("Uso:");
    }

    [Fact]
    public void Run_WithMissingFile_ReturnsTwo()
    {
        var writer = new StringWriter();

        var exitCode = Program.Run(new[] { "validate", "no-existe.json" }, writer);

        exitCode.Should().Be(2);
    }

    private static string WriteTempPack(PackDefinition pack)
    {
        var path = Path.Combine(Path.GetTempPath(), $"packtool-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, PackParser.Serialize(pack));
        return path;
    }
}
