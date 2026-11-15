using System.Text.Json;
using Ali.Core.Tests.Content;

namespace Ali.Cli.Tests;

public class ListCommandTests : IDisposable
{
    private readonly TempContentFolder folder = new();

    public ListCommandTests()
    {
        folder.Write("casos/01-agentes.md", """
            ---
            serie: casos-reales
            estado: publicado
            fecha_prevista: 2026-11-09
            fecha_publicada: 2026-11-10
            url: https://www.linkedin.com/pulse/ejemplo
            formato: articulo
            ---
            # Agentes
            """);
        folder.Write("casos/02-memoria.md", "---\nserie: casos-reales\nestado: programado\nfecha_prevista: 2026-11-17\n---\n");
        folder.Write("resumenes/01-agentes.md", "---\nserie: resumen-caso\nestado: listo\nfecha_prevista: 2026-11-19\ndepende_de: casos/01-agentes\n---\n");
        folder.Write("notas/ideas-sueltas.md", "---\nserie: notas\nestado: idea\n---\n");
        folder.Write("borradores/sin-metadatos.md", "# Sin metadatos");
    }

    public void Dispose() => folder.Dispose();

    private sealed record RunResult(int ExitCode, string Output, string Error)
    {
        public string[] OutputLines => Output.Split(Environment.NewLine);
    }

    private RunResult Run(params string[] args) => RunWith(_ => null, folder.Root, args);

    private static RunResult RunWith(Func<string, string?> environment, string currentDirectory, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApp.Run(args, output, error, environment, currentDirectory);
        return new RunResult(code, output.ToString(), error.ToString());
    }

    private RunResult List(params string[] extra) => Run(["listar", "-c", folder.Root, .. extra]);

    // ---------- CA-1 y CA-5: texto ----------

    [Fact]
    public void List_NoFilters_PrintsAlignedTableWithTotal()
    {
        var result = List();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            [
                "FECHA       ESTADO      SERIE         FICHERO",
                "2026-11-10  publicado   casos-reales  casos/01-agentes.md",
                "2026-11-17  programado  casos-reales  casos/02-memoria.md",
                "2026-11-19  listo       resumen-caso  resumenes/01-agentes.md",
                "—           —           —             borradores/sin-metadatos.md",
                "—           idea        notas         notas/ideas-sueltas.md",
                "",
                "5 piezas",
                "",
            ],
            result.OutputLines);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void List_SinglePiece_UsesSingularTotal()
    {
        Assert.EndsWith("1 pieza" + Environment.NewLine, List("-e", "idea").Output);
    }

    [Fact]
    public void List_NoMatches_PrintsMessage()
    {
        var result = List("-s", "no-existe");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("No hay piezas que cumplan los filtros." + Environment.NewLine, result.Output);
    }

    // ---------- CA-2: filtros desde la CLI ----------

    [Fact]
    public void List_CombinedFilters_ApplyAndBetweenOrWithin()
    {
        var result = List("-s", "casos-reales", "--serie", "resumen-caso", "-e", "listo", "--estado", "programado", "--hasta", "2026-11-18");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("casos/02-memoria.md", result.Output);
        Assert.DoesNotContain("casos/01-agentes.md", result.Output);
        Assert.DoesNotContain("resumenes/01-agentes.md", result.Output);
        Assert.EndsWith("1 pieza" + Environment.NewLine, result.Output);
    }

    [Fact]
    public void List_Unclassified_ShowsOnlyPiecesWithoutSeries()
    {
        var result = List("--sin-clasificar");

        Assert.Contains("borradores/sin-metadatos.md", result.Output);
        Assert.EndsWith("1 pieza" + Environment.NewLine, result.Output);
    }

    [Fact]
    public void List_StatusIsCaseInsensitive()
    {
        Assert.EndsWith("1 pieza" + Environment.NewLine, List("-e", "PUBLICADO").Output);
    }

    // ---------- CA-6: JSON ----------

    [Fact]
    public void List_Json_ProducesPiecesAndWarningsWithSpecFields()
    {
        folder.Write("rota.md", "---\nserie: s\nestado: hecho\n---\n");

        var result = List("--json", "-s", "casos-reales", "-s", "resumen-caso");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);

        using var document = JsonDocument.Parse(result.Output);
        var root = document.RootElement;

        var pieces = root.GetProperty("piezas").EnumerateArray().ToList();
        Assert.Equal(
            ["casos/01-agentes.md", "casos/02-memoria.md", "resumenes/01-agentes.md"],
            pieces.Select(p => p.GetProperty("fichero").GetString()));

        var first = pieces[0];
        Assert.Equal("casos-reales", first.GetProperty("serie").GetString());
        Assert.Equal("publicado", first.GetProperty("estado").GetString());
        Assert.Equal("2026-11-10", first.GetProperty("fecha").GetString());
        Assert.Equal("2026-11-09", first.GetProperty("fecha_prevista").GetString());
        Assert.Equal("2026-11-10", first.GetProperty("fecha_publicada").GetString());
        Assert.Equal("https://www.linkedin.com/pulse/ejemplo", first.GetProperty("url").GetString());
        Assert.Equal("articulo", first.GetProperty("formato").GetString());
        Assert.Empty(first.GetProperty("depende_de").EnumerateArray());

        var second = pieces[1];
        Assert.Equal(JsonValueKind.Null, second.GetProperty("fecha_publicada").ValueKind);
        Assert.Equal(JsonValueKind.Null, second.GetProperty("url").ValueKind);

        Assert.Equal(["casos/01-agentes"], pieces[2].GetProperty("depende_de").EnumerateArray().Select(d => d.GetString()));

        var warning = Assert.Single(root.GetProperty("avisos").EnumerateArray());
        Assert.Equal("rota.md", warning.GetProperty("fichero").GetString());
        Assert.Equal("InvalidStatus", warning.GetProperty("codigo").GetString());
        Assert.Equal("estado", warning.GetProperty("campo").GetString());
        Assert.Contains("hecho", warning.GetProperty("motivo").GetString());
    }

    [Fact]
    public void List_Json_KeepsAccentsUnescaped()
    {
        folder.Write("rota.md", "---\nfecha_prevista: mañana\n---\n");

        var output = List("--json").Output;

        Assert.Contains("mañana", output);
        Assert.Contains("válida", output);
    }

    // ---------- CA-7: avisos en stderr ----------

    [Fact]
    public void List_Text_WarningsGoToErrorOnly()
    {
        folder.Write("rota.md", "---\nserie: s\nestado: hecho\n---\n");

        var result = List("-s", "notas");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("aviso", result.Output);
        Assert.DoesNotContain("rota.md", result.Output);
        Assert.Equal(
            "aviso: rota.md: Estado 'hecho' no válido. Valores admitidos: idea, borrador, listo, programado, publicado." + Environment.NewLine,
            result.Error);
    }

    // ---------- CA-8: errores ----------

    [Fact]
    public void List_MissingFolder_FailsWithMessage()
    {
        var missing = Path.Combine(folder.Root, "no-existe");

        var result = Run("listar", "-c", missing);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: No existe la carpeta de contenido: {missing}" + Environment.NewLine, result.Error);
        Assert.Equal(string.Empty, result.Output);
    }

    [Theory]
    [InlineData(new[] { "-e", "terminado" }, "error: Estado 'terminado' no válido. Valores admitidos: idea, borrador, listo, programado, publicado.")]
    [InlineData(new[] { "--desde", "17/11/2026" }, "error: Fecha '17/11/2026' no válida en --desde. Formato esperado: AAAA-MM-DD.")]
    [InlineData(new[] { "--hasta", "2026-02-30" }, "error: Fecha '2026-02-30' no válida en --hasta. Formato esperado: AAAA-MM-DD.")]
    [InlineData(new[] { "--desde", "2026-12-01", "--hasta", "2026-11-01" }, "error: La fecha 'desde' (2026-12-01) es posterior a 'hasta' (2026-11-01).")]
    public void List_InvalidArguments_FailWithSpanishMessage(string[] extra, string message)
    {
        var result = List(extra);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(message + Environment.NewLine, result.Error);
        Assert.Equal(string.Empty, result.Output);
    }

    [Fact]
    public void List_UnknownOption_Fails()
    {
        var result = List("--hola");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--hola", result.Error);
    }

    [Fact]
    public void List_WithWarnings_ExitsWithZero()
    {
        folder.Write("rota.md", "---\nserie: [\n---\n");

        Assert.Equal(0, List().ExitCode);
    }

    // ---------- CA-9: carpeta de contenido ----------

    [Fact]
    public void List_FolderFromOption_WinsOverEnvironmentAndCurrentDirectory()
    {
        using var other = new TempContentFolder();

        var result = RunWith(_ => other.Root, other.Root, "listar", "-c", folder.Root);

        Assert.EndsWith("5 piezas" + Environment.NewLine, result.Output);
    }

    [Fact]
    public void List_FolderFromEnvironment_WinsOverCurrentDirectory()
    {
        using var other = new TempContentFolder();

        var result = RunWith(
            name => name == "ALI_CONTENIDO" ? folder.Root : null,
            other.Root,
            "listar");

        Assert.EndsWith("5 piezas" + Environment.NewLine, result.Output);
    }

    [Fact]
    public void List_NoOptionNorEnvironment_UsesCurrentDirectory()
    {
        var result = RunWith(_ => null, folder.Root, "listar");

        Assert.EndsWith("5 piezas" + Environment.NewLine, result.Output);
    }

    [Fact]
    public void List_RelativeFolder_IsResolvedAgainstCurrentDirectory()
    {
        var result = RunWith(_ => null, Path.GetDirectoryName(folder.Root)!, "listar", "-c", Path.GetFileName(folder.Root));

        Assert.EndsWith("5 piezas" + Environment.NewLine, result.Output);
    }
}
