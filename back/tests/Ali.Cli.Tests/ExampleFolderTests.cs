using Ali.Core.Content;
using Ali.Core.Scheduling;
using Ali.Core.Tests.Content;

namespace Ali.Cli.Tests;

/// <summary>
/// HU-09: the 'ejemplo/' folder loads cleanly and every output quoted in docs/manual.md is the real one.
/// </summary>
public class ExampleFolderTests
{
    // The manual uses '--desde 2026-11-15' (a Sunday); the clock is fixed to the same day.
    private static readonly DateOnly FixedToday = new(2026, 11, 15);

    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string ExampleFolder = Path.Combine(RepositoryRoot, "ejemplo");
    private static readonly string Manual = Normalize(File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "manual.md")));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) && Directory.Exists(Path.Combine(directory.FullName, "ejemplo")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd('\n');

    private static string Run(string folder, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApp.Run([.. args, "-c", folder], output, error, _ => null, folder, () => FixedToday);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal(0, code);
        return Normalize(output.ToString());
    }

    private static void AssertQuotedInManual(string output)
    {
        Assert.True(Manual.Contains(output, StringComparison.Ordinal), $"El manual no contiene esta salida:\n{output}");
    }

    // ---------- CA-3 ----------

    [Fact]
    public void Example_LoadsWithoutWarningsOrErrors()
    {
        var catalog = new CatalogReader().Read(ExampleFolder);
        var config = new SeriesConfigLoader().Load(Path.Combine(ExampleFolder, "series.yaml"));

        Assert.Equal(7, catalog.Pieces.Count);
        Assert.Empty(catalog.Warnings);
        Assert.Equal(["casos-reales", "fundamentos", "resumen-caso", "notas"], config.Series.Select(s => s.Id));
        Assert.All(catalog.Pieces.Where(p => p.Series is not null), p => Assert.NotNull(config.Find(p.Series!)));
    }

    // ---------- CA-4: lectura ----------

    [Theory]
    [InlineData("listar")]
    [InlineData("listar -e programado -e listo")]
    [InlineData("huecos casos-reales --desde 2026-11-15 -n 3")]
    [InlineData("huecos resumen-caso --desde 2026-11-15")]
    [InlineData("validar --desde 2026-11-15 --hasta 2026-11-30")]
    public void Example_ReadOutputs_AreQuotedInManual(string commandLine)
    {
        AssertQuotedInManual(Run(ExampleFolder, commandLine.Split(' ')));
    }

    // ---------- CA-4: escritura, sobre una copia ----------

    [Fact]
    public void Example_WriteOutputs_AreQuotedInManual()
    {
        using var copy = new TempContentFolder();
        foreach (var file in Directory.EnumerateFiles(ExampleFolder, "*", SearchOption.AllDirectories))
        {
            copy.WriteBytes(Path.GetRelativePath(ExampleFolder, file).Replace('\\', '/'), File.ReadAllBytes(file));
        }

        AssertQuotedInManual(Run(copy.Root, "crear", "casos-reales", "Evaluar agentes con trazas", "--fecha-libre", "--desde", "2026-11-15"));
        AssertQuotedInManual(Run(
            copy.Root,
            "estado", "casos/02-memoria-de-los-agentes", "-e", "publicado", "--fecha-publicada", "2026-11-17",
            "--url", "https://www.linkedin.com/pulse/ejemplo-memoria"));
    }
}
