using System.Text.Json;
using Ali.Core.Tests.Content;

namespace Ali.Cli.Tests;

public class SlotsCommandTests : IDisposable
{
    // A Sunday.
    private static readonly DateOnly FixedToday = new(2026, 11, 15);
    private static readonly string Nl = Environment.NewLine;

    private const string SeriesYaml = """
        series:
          - id: casos-reales
            nombre: Casos reales
            cadencia:
              dias: [martes]
              frecuencia: semanal
          - id: fundamentos
            nombre: Fundamentos
            cadencia:
              dias: [lunes, jueves]
              frecuencia: quincenal
              desde: 2026-11-02
          - id: resumen-caso
            nombre: Resumen de un caso
            despues_de:
              serie: casos-reales
              dias: 2
          - id: en-miercoles
            nombre: Miércoles
            cadencia:
              dias: miércoles
              frecuencia: semanal
            despues_de:
              serie: casos-reales
              dias: 2
          - id: notas
            nombre: Notas sueltas
        """;

    private readonly TempContentFolder folder = new();

    public SlotsCommandTests()
    {
        folder.Write("series.yaml", SeriesYaml);
        folder.Write("casos/01-agentes.md", "---\nserie: casos-reales\nestado: publicado\nfecha_publicada: 2026-11-17\n---\n");
        folder.Write("casos/02-memoria.md", "---\nserie: casos-reales\nestado: programado\nfecha_prevista: 2026-11-24\n---\n");
        folder.Write("casos/03-borrador.md", "---\nserie: casos-reales\nestado: borrador\nfecha_prevista: 2026-12-01\n---\n");
        folder.Write("resumenes/01-agentes.md", "---\nserie: resumen-caso\nestado: idea\ndepende_de: casos/01-agentes\n---\n");
        folder.Write("fundamentos/03-tokens.md", "---\nserie: fundamentos\nestado: listo\nfecha_prevista: 2026-12-01\n---\n");
    }

    public void Dispose() => folder.Dispose();

    private sealed record RunResult(int ExitCode, string Output, string Error)
    {
        public string[] OutputLines => Output.Split(Nl);
    }

    private RunResult Run(Func<string, string?> environment, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApp.Run(args, output, error, environment, folder.Root, () => FixedToday);
        return new RunResult(code, output.ToString(), error.ToString());
    }

    private RunResult Slots(params string[] extra) => Run(_ => null, ["huecos", .. extra, "-c", folder.Root]);

    // ---------- CA-8: texto ----------

    [Fact]
    public void Slots_Defaults_TodayFiveSlotsAndSeriesFileInContentFolder()
    {
        var result = Slots("casos-reales");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            [
                "Huecos de casos-reales (martes, semanal) desde 2026-11-15:",
                "",
                "FECHA       DÍA     COINCIDE CON",
                "2026-12-01  martes  fundamentos/03-tokens.md",
                "2026-12-08  martes  —",
                "2026-12-15  martes  —",
                "2026-12-22  martes  —",
                "2026-12-29  martes  —",
                "",
            ],
            result.OutputLines);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Slots_FromAndCount_AreApplied()
    {
        var result = Slots("fundamentos", "--desde", "2026-12-20", "-n", "2");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            [
                "Huecos de fundamentos (lunes y jueves, quincenal) desde 2026-12-20:",
                "",
                "FECHA       DÍA     COINCIDE CON",
                "2026-12-28  lunes   —",
                "2026-12-31  jueves  —",
                "",
            ],
            result.OutputLines);
    }

    [Fact]
    public void Slots_After_ShowsRelatedPiece()
    {
        var result = Slots("resumen-caso", "--desde", "2026-11-01");

        Assert.Equal(
            [
                "Huecos de resumen-caso (2 días después de casos-reales) desde 2026-11-01:",
                "",
                "FECHA       DÍA     TRAS                 COINCIDE CON",
                "2026-11-26  jueves  casos/02-memoria.md  —",
                "",
            ],
            result.OutputLines);
    }

    [Fact]
    public void Slots_AfterWithCadenceMismatch_ShowsWarningColumn()
    {
        var result = Slots("en-miercoles", "--desde", "hoy");

        Assert.Equal(
            [
                "Huecos de en-miercoles (2 días después de casos-reales; cadencia miércoles, semanal) desde 2026-11-15:",
                "",
                "FECHA       DÍA     TRAS                 COINCIDE CON  AVISO",
                "2026-11-19  jueves  casos/01-agentes.md  —             La fecha no coincide con la cadencia de la serie.",
                "2026-11-26  jueves  casos/02-memoria.md  —             La fecha no coincide con la cadencia de la serie.",
                "",
            ],
            result.OutputLines);
    }

    [Fact]
    public void Slots_NothingPending_PrintsMessage()
    {
        var result = Slots("resumen-caso", "--desde", "2027-01-01");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"No hay huecos pendientes para resumen-caso desde 2027-01-01.{Nl}", result.Output);
    }

    [Fact]
    public void Slots_CatalogWarnings_GoToError()
    {
        folder.Write("rota.md", "---\nestado: hecho\n---\n");

        var result = Slots("casos-reales", "-n", "1");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("aviso: rota.md: Estado 'hecho' no válido.", result.Error);
        Assert.DoesNotContain("rota.md", result.Output);
    }

    // ---------- CA-8: JSON ----------

    [Fact]
    public void Slots_Json_HasSpecFields()
    {
        folder.Write("rota.md", "---\nestado: hecho\n---\n");

        var result = Slots("en-miercoles", "--json", "-n", "1");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        using var document = JsonDocument.Parse(result.Output);
        var root = document.RootElement;
        Assert.Equal("en-miercoles", root.GetProperty("serie").GetString());
        Assert.Equal("2026-11-15", root.GetProperty("desde").GetString());

        var slot = Assert.Single(root.GetProperty("huecos").EnumerateArray());
        Assert.Equal("2026-11-19", slot.GetProperty("fecha").GetString());
        Assert.Equal("jueves", slot.GetProperty("dia").GetString());
        Assert.Equal("casos/01-agentes.md", slot.GetProperty("tras").GetString());
        Assert.Empty(slot.GetProperty("coincide_con").EnumerateArray());
        Assert.Equal("La fecha no coincide con la cadencia de la serie.", slot.GetProperty("aviso").GetString());

        var warning = Assert.Single(root.GetProperty("avisos").EnumerateArray());
        Assert.Equal("rota.md", warning.GetProperty("fichero").GetString());
    }

    [Fact]
    public void Slots_Json_NullsForCadenceSeries()
    {
        var result = Slots("casos-reales", "--json", "-n", "1");

        using var document = JsonDocument.Parse(result.Output);
        var slot = Assert.Single(document.RootElement.GetProperty("huecos").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, slot.GetProperty("tras").ValueKind);
        Assert.Equal(JsonValueKind.Null, slot.GetProperty("aviso").ValueKind);
        Assert.Equal(["fundamentos/03-tokens.md"], slot.GetProperty("coincide_con").EnumerateArray().Select(e => e.GetString()));
    }

    // ---------- Fichero de series ----------

    [Fact]
    public void Slots_SeriesFileFromOption_WinsOverEnvironment()
    {
        using var other = new TempContentFolder();
        var file = other.Write("otras.yaml", "series:\n  - id: solo\n    nombre: Solo\n    cadencia:\n      dias: viernes\n      frecuencia: semanal\n");

        var result = Run(_ => Path.Combine(folder.Root, "series.yaml"), "huecos", "solo", "-n", "1", "-c", folder.Root, "--series", file);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("2026-11-20  viernes", result.Output);
    }

    [Fact]
    public void Slots_SeriesFileFromEnvironment_WinsOverDefault()
    {
        using var other = new TempContentFolder();
        var file = other.Write("otras.yaml", "series:\n  - id: solo\n    nombre: Solo\n    cadencia:\n      dias: viernes\n      frecuencia: semanal\n");

        var result = Run(name => name == "ALI_SERIES" ? file : null, "huecos", "solo", "-n", "1", "-c", folder.Root);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("2026-11-20  viernes", result.Output);
    }

    // ---------- CA-8: errores ----------

    [Theory]
    [InlineData(new[] { "notas" }, "La serie 'notas' no tiene cadencia; no se pueden calcular huecos.")]
    [InlineData(new[] { "casos" }, "La serie 'casos' no existe en el fichero de series. Series disponibles: casos-reales, fundamentos, resumen-caso, en-miercoles, notas.")]
    [InlineData(new[] { "casos-reales", "--desde", "15/11/2026" }, "Fecha '15/11/2026' no válida en --desde. Formato esperado: AAAA-MM-DD u hoy.")]
    [InlineData(new[] { "casos-reales", "-n", "0" }, "La cantidad debe ser un entero entre 1 y 100.")]
    [InlineData(new[] { "casos-reales", "-n", "101" }, "La cantidad debe ser un entero entre 1 y 100.")]
    [InlineData(new[] { "casos-reales", "--cantidad", "cinco" }, "La cantidad debe ser un entero entre 1 y 100.")]
    public void Slots_InvalidRequest_FailsWithMessage(string[] extra, string message)
    {
        var result = Slots(extra);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: {message}{Nl}", result.Error);
        Assert.Equal(string.Empty, result.Output);
    }

    [Fact]
    public void Slots_MissingSeriesFile_Fails()
    {
        File.Delete(Path.Combine(folder.Root, "series.yaml"));

        var result = Slots("casos-reales");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"No existe el fichero de series: {Path.Combine(folder.Root, "series.yaml")}", result.Error);
    }

    [Fact]
    public void Slots_InvalidSeriesFile_ReportsErrors()
    {
        folder.Write("series.yaml", "series:\n  - id: Mal\n");

        var result = Slots("casos-reales");

        Assert.Equal(1, result.ExitCode);
        Assert.StartsWith("error: El fichero de series", result.Error);
        Assert.Contains("'Mal' no es válido", result.Error);
    }

    [Fact]
    public void Slots_MissingContentFolder_Fails()
    {
        var missing = Path.Combine(folder.Root, "no-existe");

        var result = Run(_ => null, "huecos", "casos-reales", "-c", missing);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: No existe la carpeta de contenido: {missing}{Nl}", result.Error);
    }
}
