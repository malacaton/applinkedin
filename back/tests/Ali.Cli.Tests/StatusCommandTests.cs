using Ali.Core.Tests.Content;

namespace Ali.Cli.Tests;

public class StatusCommandTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 11, 18);
    private static readonly string Nl = Environment.NewLine;

    private readonly TempContentFolder folder = new();

    public StatusCommandTests()
    {
        folder.Write("casos/02-memoria.md", "---\nserie: casos-reales\nestado: programado\nfecha_prevista: 2026-11-17\n---\n# Memoria\n");
    }

    public void Dispose() => folder.Dispose();

    private sealed record RunResult(int ExitCode, string Output, string Error);

    private RunResult Run(Func<string, string?> environment, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApp.Run(args, output, error, environment, folder.Root, () => FixedToday);
        return new RunResult(code, output.ToString(), error.ToString());
    }

    private RunResult Status(params string[] extra) => Run(_ => null, ["estado", .. extra, "-c", folder.Root]);

    private string PieceText() => File.ReadAllText(Path.Combine(folder.Root, "casos", "02-memoria.md"));

    // ---------- CA-8: salida ----------

    [Fact]
    public void Status_WithChanges_PrintsEachChangeAndWrites()
    {
        var result = Status("casos/02-memoria", "-e", "publicado", "--fecha-publicada", "hoy");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            $"Actualizado casos/02-memoria.md:{Nl}  estado: programado → publicado{Nl}  fecha_publicada: — → 2026-11-18{Nl}",
            result.Output);
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(
            "---\nserie: casos-reales\nestado: publicado\nfecha_prevista: 2026-11-17\nfecha_publicada: 2026-11-18\n---\n# Memoria\n",
            PieceText());
    }

    [Fact]
    public void Status_AllOptions_AreApplied()
    {
        var result = Status(
            "casos\\02-memoria.md",
            "--estado", "LISTO",
            "--fecha-prevista", "2026-11-24",
            "--fecha-publicada", "HOY",
            "--url", " https://www.linkedin.com/pulse/x ");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            "---\nserie: casos-reales\nestado: listo\nfecha_prevista: 2026-11-24\nfecha_publicada: 2026-11-18\nurl: https://www.linkedin.com/pulse/x\n---\n# Memoria\n",
            PieceText());
    }

    [Fact]
    public void Status_SameValues_PrintsNoChanges()
    {
        var result = Status("casos/02-memoria.md", "-e", "programado");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"Sin cambios en casos/02-memoria.md.{Nl}", result.Output);
    }

    [Fact]
    public void Status_FolderFromEnvironment_IsUsed()
    {
        var result = Run(name => name == "ALI_CONTENIDO" ? folder.Root : null, "estado", "casos/02-memoria", "-e", "listo");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("estado: listo", PieceText());
    }

    // ---------- CA-8: errores ----------

    [Theory]
    [InlineData(new[] { "casos/02-memoria", "-e", "hecho" }, "Estado 'hecho' no válido. Valores admitidos: idea, borrador, listo, programado, publicado.")]
    [InlineData(new[] { "casos/02-memoria", "--fecha-prevista", "mañana" }, "Fecha 'mañana' no válida en --fecha-prevista. Formato esperado: AAAA-MM-DD u hoy.")]
    [InlineData(new[] { "casos/02-memoria", "--fecha-publicada", "2026-02-30" }, "Fecha '2026-02-30' no válida en --fecha-publicada. Formato esperado: AAAA-MM-DD u hoy.")]
    [InlineData(new[] { "casos/02-memoria", "--url", "linkedin.com/x" }, "URL 'linkedin.com/x' no válida: debe empezar por http:// o https://.")]
    [InlineData(new[] { "casos/02-memoria", "-e", "publicado" }, "Para marcar como publicado hace falta --fecha-publicada (la pieza no tiene fecha_publicada).")]
    [InlineData(new[] { "casos/02-memoria" }, "Indica al menos un campo que cambiar: --estado, --fecha-prevista, --fecha-publicada o --url.")]
    [InlineData(new[] { "../fuera", "-e", "listo" }, "La pieza debe estar dentro de la carpeta de contenido.")]
    public void Status_InvalidRequest_FailsWithoutTouchingTheFile(string[] extra, string message)
    {
        var before = PieceText();

        var result = Status(extra);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: {message}{Nl}", result.Error);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal(before, PieceText());
    }

    [Fact]
    public void Status_MissingPiece_FailsWithMessage()
    {
        var result = Status("casos/03-otra", "-e", "listo");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: No existe la pieza 'casos/03-otra.md' en {folder.Root}.{Nl}", result.Error);
    }

    [Fact]
    public void Status_MissingFolder_FailsWithMessage()
    {
        var missing = Path.Combine(folder.Root, "no-existe");

        var result = Run(_ => null, "estado", "casos/02-memoria", "-e", "listo", "-c", missing);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: No existe la carpeta de contenido: {missing}{Nl}", result.Error);
    }

    [Fact]
    public void Status_MissingPieceArgument_Fails()
    {
        var result = Run(_ => null, "estado", "-e", "listo", "-c", folder.Root);

        Assert.Equal(1, result.ExitCode);
        Assert.NotEqual(string.Empty, result.Error);
    }
}
