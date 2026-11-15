using Ali.Core.Tests.Content;

namespace Ali.Cli.Tests;

public class CreateCommandTests : IDisposable
{
    // A Sunday; the next Tuesdays are 11-17 and 11-24.
    private static readonly DateOnly FixedToday = new(2026, 11, 15);
    private static readonly string Nl = Environment.NewLine;

    private const string SeriesYaml = """
        series:
          - id: casos-reales
            nombre: Casos reales
            carpeta: casos
            cadencia:
              dias: martes
              frecuencia: semanal
          - id: resumen-caso
            nombre: Resumen de un caso
            carpeta: resumenes
            despues_de:
              serie: casos-reales
              dias: 2
        """;

    private readonly TempContentFolder folder = new();

    public CreateCommandTests()
    {
        folder.Write("series.yaml", SeriesYaml);
        folder.Write("casos/01-agentes.md", "---\nserie: casos-reales\nestado: publicado\nfecha_publicada: 2026-11-17\n---\n");
    }

    public void Dispose() => folder.Dispose();

    private sealed record RunResult(int ExitCode, string Output, string Error);

    private RunResult Create(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApp.Run(["crear", .. args, "-c", folder.Root], output, error, _ => null, folder.Root, () => FixedToday);
        return new RunResult(code, output.ToString(), error.ToString());
    }

    private string ReadText(string path) => File.ReadAllText(Path.Combine(folder.Root, path));

    // ---------- CA-7 ----------

    [Fact]
    public void Create_Minimal_PrintsPathAndFields()
    {
        var result = Create("casos-reales", "Qué aprendimos del RAG");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal($"Creada casos/02-que-aprendimos-del-rag.md{Nl}  serie: casos-reales{Nl}  estado: idea{Nl}", result.Output);
        Assert.Equal("---\nserie: casos-reales\nestado: idea\n---\n# Qué aprendimos del RAG\n", ReadText("casos/02-que-aprendimos-del-rag.md"));
    }

    [Fact]
    public void Create_FirstFreeDate_WithFormat()
    {
        var result = Create("casos-reales", "Memoria", "--fecha-libre", "--formato", "articulo");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            $"Creada casos/02-memoria.md{Nl}  serie: casos-reales{Nl}  estado: idea{Nl}  fecha_prevista: 2026-11-24{Nl}  formato: articulo{Nl}",
            result.Output);
    }

    [Fact]
    public void Create_FirstFreeDate_AfterSeries_ShowsDependency()
    {
        var result = Create("resumen-caso", "Resumen agentes", "--fecha-libre", "--desde", "2026-11-01");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            $"Creada resumenes/01-resumen-agentes.md{Nl}  serie: resumen-caso{Nl}  estado: idea{Nl}  fecha_prevista: 2026-11-19{Nl}  depende_de: casos/01-agentes{Nl}",
            result.Output);
    }

    [Fact]
    public void Create_ExplicitDateAndDependencies()
    {
        var result = Create("resumen-caso", "Otro", "--fecha", "hoy", "--depende-de", "casos/01-agentes", "--depende-de", "casos/00-intro");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("fecha_prevista: 2026-11-15\ndepende_de:\n  - casos/01-agentes\n  - casos/00-intro\n", ReadText("resumenes/01-otro.md"));
    }

    [Fact]
    public void Create_ExistingFile_FailsWithoutOverwriting()
    {
        folder.Write("casos/02-repetida.md", "previo");

        var result = Create("casos-reales", "Repetida");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: Ya existe 'casos/02-repetida.md'; no se sobrescribe.{Nl}", result.Error);
        Assert.Equal("previo", ReadText("casos/02-repetida.md"));
    }

    [Theory]
    [InlineData(new[] { "casos-reales", "X", "--fecha", "mañana" }, "Fecha 'mañana' no válida en --fecha. Formato esperado: AAAA-MM-DD u hoy.")]
    [InlineData(new[] { "casos-reales", "X", "--fecha-libre", "--desde", "1/1/2027" }, "Fecha '1/1/2027' no válida en --desde. Formato esperado: AAAA-MM-DD u hoy.")]
    [InlineData(new[] { "casos-reales", "X", "--fecha", "hoy", "--fecha-libre" }, "Usa --fecha o --fecha-libre, no las dos.")]
    [InlineData(new[] { "casos-reales", "¿?" }, "El título no contiene letras ni dígitos.")]
    [InlineData(new[] { "notas", "X" }, "La serie 'notas' no existe en el fichero de series. Series disponibles: casos-reales, resumen-caso.")]
    [InlineData(new[] { "resumen-caso", "X", "--fecha-libre", "--desde", "2027-01-01" }, "No hay huecos libres para resumen-caso desde 2027-01-01.")]
    public void Create_InvalidRequest_FailsWithMessage(string[] args, string message)
    {
        var result = Create(args);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: {message}{Nl}", result.Error);
        Assert.Equal(string.Empty, result.Output);
        Assert.False(Directory.Exists(Path.Combine(folder.Root, "resumenes")));
    }

    [Fact]
    public void Create_MissingTitle_Fails()
    {
        var result = Create("casos-reales");

        Assert.Equal(1, result.ExitCode);
        Assert.NotEqual(string.Empty, result.Error);
    }
}
