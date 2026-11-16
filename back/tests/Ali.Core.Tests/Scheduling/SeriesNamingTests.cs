using Ali.Core.Scheduling;
using Ali.Core.Tests.Content;

namespace Ali.Core.Tests.Scheduling;

/// <summary>
/// HU-07: 'carpeta' and 'fichero' in the series file, and the file name template.
/// </summary>
public class SeriesNamingTests : IDisposable
{
    private readonly TempContentFolder folder = new();
    private readonly SeriesConfigLoader loader = new();

    public void Dispose() => folder.Dispose();

    private SeriesConfig Load(string yaml) => loader.Load(folder.Write("series.yaml", yaml));

    private SeriesConfigError SingleError(string seriesBody) =>
        Assert.Single(Assert.Throws<SeriesConfigException>(() => Load("series:\n" + seriesBody)).Errors);

    // ---------- CA-6: carga ----------

    [Fact]
    public void Load_WithoutNamingFields_UsesDefaults()
    {
        var series = Load("series:\n  - id: casos-reales\n    nombre: Casos\n").Find("casos-reales")!;

        Assert.Null(series.Folder);
        Assert.Null(series.FileName);
        Assert.Equal("casos-reales", series.ResolvedFolder);
        Assert.Equal("{nn}-{titulo}", series.ResolvedFileName.Text);
    }

    [Fact]
    public void Load_NamingFields_AreRead()
    {
        var series = Load("""
            series:
              - id: casos-reales
                nombre: Casos
                carpeta: 'contenido\casos/'
                fichero: "{fecha}_{nnn}_{titulo}"
            """).Find("casos-reales")!;

        Assert.Equal("contenido/casos", series.ResolvedFolder);
        Assert.Equal("{fecha}_{nnn}_{titulo}", series.ResolvedFileName.Text);
        Assert.True(series.ResolvedFileName.UsesDate);
        Assert.True(series.ResolvedFileName.UsesNumber);
    }

    [Theory]
    [InlineData("../fuera")]
    [InlineData("casos/../../fuera")]
    [InlineData("/absoluta")]
    [InlineData("C:/absoluta")]
    [InlineData(".oculta")]
    [InlineData("casos/.oculta")]
    public void Load_InvalidFolder_IsError(string value)
    {
        var error = SingleError($"  - id: x\n    nombre: X\n    carpeta: '{value}'\n");

        Assert.Equal("Serie 'x', campo 'carpeta': debe ser una ruta relativa dentro de la carpeta de contenido.", error.ToString());
    }

    [Theory]
    [InlineData("{nn}", "debe incluir {titulo} o {titulo_original}.")]
    [InlineData("{numero}-{titulo}", "marcador '{numero}' desconocido; usa {n}, {nn}, {nnn}, {titulo}, {titulo_original}, {fecha} o {aammdd}.")]
    [InlineData("{nn-{titulo}", "marcador '{nn-{titulo}' desconocido; usa {n}, {nn}, {nnn}, {titulo}, {titulo_original}, {fecha} o {aammdd}.")]
    [InlineData("{titulo", "tiene una '{' sin cerrar.")]
    [InlineData("titulo}{titulo}", "tiene una '}' sin su '{'.")]
    [InlineData("sub/{titulo}", "solo puede contener el nombre del fichero, sin carpetas ni caracteres reservados.")]
    [InlineData(".{titulo}", "solo puede contener el nombre del fichero, sin carpetas ni caracteres reservados.")]
    public void Load_InvalidFileName_IsError(string value, string reason)
    {
        var error = SingleError($"  - id: x\n    nombre: X\n    fichero: \"{value}\"\n");

        Assert.Equal("fichero", error.Field);
        Assert.Equal(reason, error.Reason);
    }

    // ---------- Plantilla ----------

    [Theory]
    [InlineData("{nn}-{titulo}", 5, "05-hola")]
    [InlineData("{nn}-{titulo}", 123, "123-hola")]
    [InlineData("{n}-{titulo}", 7, "7-hola")]
    [InlineData("{nnn}_{titulo}", 7, "007_hola")]
    [InlineData("{fecha}-{titulo}", 1, "2026-12-01-hola")]
    [InlineData("{titulo}", 9, "hola")]
    public void Template_Resolves(string text, int number, string expected)
    {
        Assert.True(FileNameTemplate.TryParse(text, out var template, out _));

        Assert.Equal(expected, template!.Resolve(number, "hola", new DateOnly(2026, 12, 1)));
    }

    [Fact]
    public void Template_WithoutNumber_DoesNotUseNumber()
    {
        Assert.True(FileNameTemplate.TryParse("{fecha}-{titulo}", out var template, out _));

        Assert.False(template!.UsesNumber);
        Assert.True(template.UsesDate);
        Assert.False(FileNameTemplate.Default.UsesDate);
    }
}
