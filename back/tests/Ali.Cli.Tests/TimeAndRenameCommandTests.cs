using Ali.Core.Tests.Content;

namespace Ali.Cli.Tests;

/// <summary>
/// HU-11 from the command line: --hora in 'estado' and 'crear', and renaming when the date changes.
/// </summary>
public class TimeAndRenameCommandTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 11, 15);
    private static readonly string Nl = Environment.NewLine;

    private readonly TempContentFolder folder = new();

    public TimeAndRenameCommandTests()
    {
        folder.Write("series.yaml", """
            series:
              - id: principal
                nombre: Principal
                carpeta: "."
                fichero: "{nn} - Post {n} - {aammdd} - {titulo_original}"
                cadencia: { dias: martes, frecuencia: semanal, hora: "08:00" }
            """);
        folder.Write("15 - Post 15 - 261124 - Segundo post.md", "---\nserie: principal\nestado: listo\nfecha_prevista: 2026-11-24\n---\n# Segundo\n");
        folder.Write("15 - Post 15 - 261124 - TIENE IMAGEN - Alt.txt", "");
    }

    public void Dispose() => folder.Dispose();

    private sealed record RunResult(int ExitCode, string Output, string Error);

    private RunResult Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApp.Run([.. args, "-c", folder.Root], output, error, _ => null, folder.Root, () => FixedToday);
        return new RunResult(code, output.ToString(), error.ToString());
    }

    private string[] Files() => Directory.GetFiles(folder.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;

    [Fact]
    public void Estado_DateAndTime_RenamesAndReports()
    {
        var result = Run("estado", "15 - Post 15 - 261124 - Segundo post", "--fecha-prevista", "2026-12-01", "--hora", "16:00");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(
            $"Actualizado 15 - Post 15 - 261124 - Segundo post.md:{Nl}"
            + $"  fecha_prevista: 2026-11-24 → 2026-12-01{Nl}"
            + $"  hora: — → 16:00{Nl}"
            + $"  renombrado: 15 - Post 15 - 261124 - Segundo post.md → 15 - Post 15 - 261201 - Segundo post.md{Nl}"
            + $"  renombrado: 15 - Post 15 - 261124 - TIENE IMAGEN - Alt.txt → 15 - Post 15 - 261201 - TIENE IMAGEN - Alt.txt{Nl}",
            result.Output);
        Assert.Equal(
            ["15 - Post 15 - 261201 - Segundo post.md", "15 - Post 15 - 261201 - TIENE IMAGEN - Alt.txt", "series.yaml"],
            Files());
    }

    [Fact]
    public void Estado_RenameBlocked_WarnsOnErrorAndKeepsFiles()
    {
        folder.Write("15 - Post 15 - 261201 - Segundo post.md", "otro");

        var result = Run("estado", "15 - Post 15 - 261124 - Segundo post", "--fecha-prevista", "2026-12-01");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"aviso: No se renombra: ya existe '15 - Post 15 - 261201 - Segundo post.md'.{Nl}", result.Error);
        Assert.Equal(4, Files().Length);
    }

    [Fact]
    public void Estado_WithoutSeriesFile_ChangesWithoutRenaming()
    {
        File.Delete(Path.Combine(folder.Root, "series.yaml"));

        var result = Run("estado", "15 - Post 15 - 261124 - Segundo post", "--fecha-prevista", "2026-12-01");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("renombrado", result.Output);
        Assert.Contains("15 - Post 15 - 261124 - Segundo post.md", Files());
    }

    [Fact]
    public void Estado_ExplicitMissingSeriesFile_Fails()
    {
        var result = Run("estado", "15 - Post 15 - 261124 - Segundo post", "--hora", "16:00", "--series", "no-existe.yaml");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No existe el fichero de series", result.Error);
    }

    [Theory]
    [InlineData("estado")]
    [InlineData("crear")]
    public void InvalidTime_FailsWithMessage(string command)
    {
        string[] args = command == "estado"
            ? ["estado", "15 - Post 15 - 261124 - Segundo post", "--hora", "4pm"]
            : ["crear", "principal", "Nuevo", "--fecha", "2026-12-01", "--hora", "4pm"];

        var result = Run(args);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: Hora '4pm' no válida en --hora. Formato esperado: HH:mm.{Nl}", result.Error);
    }

    [Fact]
    public void Crear_UsesConventionAndSeriesTime()
    {
        var result = Run("crear", "principal", "¿Cuándo NO necesitas un agente?", "--fecha-libre");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            $"Creada 16 - Post 16 - 261117 - Cuándo NO necesitas un agente.md{Nl}"
            + $"  serie: principal{Nl}  estado: idea{Nl}  fecha_prevista: 2026-11-17{Nl}  hora: 08:00{Nl}",
            result.Output);
    }

    [Fact]
    public void Listar_ShowsTimeColumn()
    {
        Run("estado", "15 - Post 15 - 261124 - Segundo post", "--hora", "16:00");

        var result = Run("listar");

        Assert.Contains("2026-11-24  16:00  listo", result.Output);
    }
}
