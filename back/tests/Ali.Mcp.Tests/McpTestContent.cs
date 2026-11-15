using Ali.Core.Tests.Content;

namespace Ali.Mcp.Tests;

/// <summary>
/// A content folder with a series file and a few pieces, shared by the MCP tests.
/// </summary>
internal sealed class McpTestContent : IDisposable
{
    // A Sunday; the next Tuesdays are 11-17, 11-24 and 12-01.
    public static readonly DateOnly Today = new(2026, 11, 15);

    public const string SeriesYaml = """
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

    public McpTestContent()
    {
        Folder.Write("series.yaml", SeriesYaml);
        Folder.Write("casos/01-agentes.md", "---\nserie: casos-reales\nestado: publicado\nfecha_publicada: 2026-11-17\nurl: https://www.linkedin.com/pulse/x\n---\n# Agentes\n");
        Folder.Write("casos/02-memoria.md", "---\nserie: casos-reales\nestado: programado\nfecha_prevista: 2026-11-24\n---\n# Memoria\n");
        Folder.Write("resumenes/01-agentes.md", "---\nserie: resumen-caso\nestado: borrador\ndepende_de: casos/01-agentes\n---\n");
        Folder.Write("rota.md", "---\nestado: hecho\n---\n");
    }

    public TempContentFolder Folder { get; } = new();

    public string Root => Folder.Root;

    public string SeriesFile => Path.Combine(Root, "series.yaml");

    public AliTools Tools() => new(new ServerSettings(Root, SeriesFile), new FixedClock(Today));

    /// <summary>
    /// Runs the CLI against the same folder and clock; returns stdout without the final line break.
    /// </summary>
    public string Cli(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Ali.Cli.CliApp.Run([.. args, "-c", Root], output, error, _ => null, Root, () => Today);
        Assert.Equal(0, code);
        return output.ToString().TrimEnd('\r', '\n');
    }

    public string ReadText(string path) => File.ReadAllText(Path.Combine(Root, path));

    public void Dispose() => Folder.Dispose();
}

/// <summary>
/// Clock stopped at noon (UTC) of a given day.
/// </summary>
internal sealed class FixedClock(DateOnly day) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
