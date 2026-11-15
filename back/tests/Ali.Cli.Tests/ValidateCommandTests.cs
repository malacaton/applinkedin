using System.Text.Json;
using System.Text.RegularExpressions;
using Ali.Core.Tests.Content;

namespace Ali.Cli.Tests;

public partial class ValidateCommandTests : IDisposable
{
    // A Sunday. Default range: 2026-11-15 to 2026-12-13; Tuesdays 11-17, 11-24, 12-01, 12-08.
    private static readonly DateOnly FixedToday = new(2026, 11, 15);
    private static readonly string Nl = Environment.NewLine;

    private const string SeriesYaml = """
        series:
          - id: casos-reales
            nombre: Casos reales
            cadencia:
              dias: martes
              frecuencia: semanal
          - id: resumen-caso
            nombre: Resumen de un caso
            despues_de:
              serie: casos-reales
              dias: 2
        """;

    private readonly TempContentFolder folder = new();

    public ValidateCommandTests()
    {
        folder.Write("series.yaml", SeriesYaml);
        folder.Write("casos/01-agentes.md", "---\nserie: casos-reales\nestado: publicado\nfecha_publicada: 2026-11-17\n---\n");
        folder.Write("casos/02-memoria.md", "---\nserie: casos-reales\nestado: programado\nfecha_prevista: 2026-11-24\n---\n");
        folder.Write("casos/03-borrador.md", "---\nserie: casos-reales\nestado: borrador\n---\n");
        folder.Write("casos/04-rag.md", "---\nserie: casos-reales\nestado: listo\nfecha_prevista: 2026-11-24\n---\n");
        folder.Write("resumenes/01-agentes.md", "---\nserie: resumen-caso\nestado: listo\nfecha_prevista: 2026-11-19\ndepende_de: casos/01-agentes\n---\n");
        folder.Write("resumenes/02-memoria.md", "---\nserie: resumen-caso\nestado: idea\ndepende_de: casos/03-borrador\n---\n");
        folder.Write("resumenes/03-roto.md", "---\nserie: resumen-caso\nestado: idea\ndepende_de: casos/09\n---\n");
    }

    public void Dispose() => folder.Dispose();

    private sealed record RunResult(int ExitCode, string Output, string Error)
    {
        public string[] OutputLines => Output.Split(Nl);
    }

    [GeneratedRegex(" {2,}")]
    private static partial Regex ColumnGap();

    private static string[] Cells(string line) => ColumnGap().Split(line);

    private RunResult Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApp.Run(args, output, error, _ => null, folder.Root, () => FixedToday);
        return new RunResult(code, output.ToString(), error.ToString());
    }

    private RunResult Validate(params string[] extra) => Run(["validar", .. extra, "-c", folder.Root]);

    // ---------- CA-8: texto ----------

    [Fact]
    public void Validate_DefaultRange_ListsProblemsInOrder()
    {
        var result = Validate();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        var lines = result.OutputLines;
        Assert.Equal("Calendario del 2026-11-15 al 2026-12-13: 7 problemas", lines[0]);
        Assert.Equal(string.Empty, lines[1]);
        Assert.Equal(["FECHA", "TIPO", "SERIE", "PIEZAS", "DETALLE"], Cells(lines[2]));
        Assert.Equal(
            [
                ["2026-11-24", "choque", "casos-reales", "casos/02-memoria.md, casos/04-rag.md", "2 piezas de casos-reales el mismo día."],
                ["2026-11-26", "hueco", "resumen-caso", "casos/02-memoria.md", "Falta la pieza de resumen-caso que va 2 días después de casos/02-memoria.md."],
                ["2026-11-26", "hueco", "resumen-caso", "casos/04-rag.md", "Falta la pieza de resumen-caso que va 2 días después de casos/04-rag.md."],
                ["2026-12-01", "hueco", "casos-reales", "—", "Toca publicar y no hay pieza."],
                ["2026-12-08", "hueco", "casos-reales", "—", "Toca publicar y no hay pieza."],
                ["—", "bloqueada", "resumen-caso", "resumenes/02-memoria.md, casos/03-borrador.md", "Depende de casos/03-borrador.md, que está en borrador."],
                ["—", "dependencia_rota", "resumen-caso", "resumenes/03-roto.md", "depende_de 'casos/09' no existe."],
            ],
            lines[3..10].Select(Cells));
        Assert.Equal(string.Empty, lines[10]);
        Assert.Equal(11, lines.Length);
    }

    [Fact]
    public void Validate_SeriesFilterAndRange_AreApplied()
    {
        var result = Validate("-s", "casos-reales", "--desde", "2026-11-20", "--hasta", "2026-12-02");

        var lines = result.OutputLines;
        Assert.Equal("Calendario del 2026-11-20 al 2026-12-02: 2 problemas", lines[0]);
        Assert.Equal(["choque", "hueco"], lines[3..5].Select(l => Cells(l)[1]));
    }

    [Fact]
    public void Validate_NoProblems_PrintsMessage()
    {
        var result = Validate("-s", "casos-reales", "--desde", "2026-11-16", "--hasta", "2026-11-18");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"Sin problemas del 2026-11-16 al 2026-11-18.{Nl}", result.Output);
    }

    [Fact]
    public void Validate_ReversedRangeFails_AndSingleProblemUsesSingular()
    {
        var result = Validate("-s", "casos-reales", "--desde", "2026-11-24", "--hasta", "hoy");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: La fecha 'desde' (2026-11-24) es posterior a 'hasta' (2026-11-15).{Nl}", result.Error);

        var single = Validate("-s", "casos-reales", "--desde", "2026-11-24", "--hasta", "2026-11-24");
        Assert.StartsWith("Calendario del 2026-11-24 al 2026-11-24: 1 problema" + Nl, single.Output);
    }

    [Fact]
    public void Validate_CatalogWarnings_GoToError()
    {
        folder.Write("rota.md", "---\nestado: hecho\n---\n");

        var result = Validate();

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("aviso: rota.md:", result.Error);
    }

    // ---------- CA-8: JSON ----------

    [Fact]
    public void Validate_Json_HasSpecFields()
    {
        var result = Validate("--json", "-s", "resumen-caso");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Output);
        var root = document.RootElement;
        Assert.Equal("2026-11-15", root.GetProperty("desde").GetString());
        Assert.Equal("2026-12-13", root.GetProperty("hasta").GetString());
        Assert.Empty(root.GetProperty("avisos").EnumerateArray());

        var problems = root.GetProperty("problemas").EnumerateArray().ToList();
        Assert.Equal(["hueco", "hueco", "bloqueada", "dependencia_rota"], problems.Select(p => p.GetProperty("tipo").GetString()));

        var gap = problems[0];
        Assert.Equal("2026-11-26", gap.GetProperty("fecha").GetString());
        Assert.Equal("resumen-caso", gap.GetProperty("serie").GetString());
        Assert.Equal(["casos/02-memoria.md"], gap.GetProperty("piezas").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("2 días después", gap.GetProperty("detalle").GetString());

        Assert.Equal(JsonValueKind.Null, problems[3].GetProperty("fecha").ValueKind);
    }

    // ---------- CA-8: errores ----------

    [Theory]
    [InlineData(new[] { "--desde", "mañana" }, "Fecha 'mañana' no válida en --desde. Formato esperado: AAAA-MM-DD u hoy.")]
    [InlineData(new[] { "--hasta", "2026-13-01" }, "Fecha '2026-13-01' no válida en --hasta. Formato esperado: AAAA-MM-DD u hoy.")]
    [InlineData(new[] { "--hasta", "2027-11-17" }, "El rango no puede superar un año (366 días).")]
    [InlineData(new[] { "-s", "casos" }, "La serie 'casos' no existe en el fichero de series. Series disponibles: casos-reales, resumen-caso.")]
    public void Validate_InvalidRequest_FailsWithMessage(string[] extra, string message)
    {
        var result = Validate(extra);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: {message}{Nl}", result.Error);
        Assert.Equal(string.Empty, result.Output);
    }

    [Fact]
    public void Validate_MissingSeriesFile_Fails()
    {
        File.Delete(Path.Combine(folder.Root, "series.yaml"));

        var result = Validate();

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No existe el fichero de series", result.Error);
    }
}
