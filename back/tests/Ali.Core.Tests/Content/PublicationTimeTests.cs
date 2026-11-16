using Ali.Core.Content;
using Ali.Core.Scheduling;

namespace Ali.Core.Tests.Content;

/// <summary>
/// HU-11: 'hora' metadata, original title cleaning and the new series fields.
/// </summary>
public class PublicationTimeTests : IDisposable
{
    private readonly TempContentFolder folder = new();

    public void Dispose() => folder.Dispose();

    private Catalog Read() => new CatalogReader().Read(folder.Root);

    // ---------- CA-5: lectura de la hora ----------

    [Theory]
    [InlineData("hora: \"16:00\"", 16, 0)]
    [InlineData("hora: 08:30", 8, 30)]
    [InlineData("hora: '9:05'", 9, 5)]
    public void Read_ValidTime(string line, int hour, int minute)
    {
        folder.Write("p.md", $"---\nserie: s\n{line}\n---\n");

        var catalog = Read();

        Assert.Equal(new TimeOnly(hour, minute), Assert.Single(catalog.Pieces).Time);
        Assert.Empty(catalog.Warnings);
    }

    [Fact]
    public void Read_InvalidTime_WarnsAndCountsAsAbsent()
    {
        folder.Write("p.md", "---\nserie: s\nhora: \"25:00\"\n---\n");

        var catalog = Read();

        Assert.Null(Assert.Single(catalog.Pieces).Time);
        var warning = Assert.Single(catalog.Warnings);
        Assert.Equal(CatalogWarningCode.InvalidTime, warning.Code);
        Assert.Equal("hora", warning.Field);
        Assert.Equal("Hora '25:00' no válida en 'hora'. Formato esperado: HH:mm.", warning.Message);
    }

    // ---------- CA-2: título original ----------

    [Theory]
    [InlineData("¿Por qué existe esta función?", "Por qué existe esta función")]
    [InlineData("¡Hola!  Mundo:  a/b\\c *x* \"y\" <z>|", "Hola! Mundo abc x y z")]
    [InlineData("  Termina en punto.  ", "Termina en punto")]
    [InlineData("Año, pingüino y ÑANDÚ", "Año, pingüino y ÑANDÚ")]
    [InlineData("¿?", "")]
    public void Original_RemovesOnlyForbiddenCharacters(string title, string expected)
    {
        Assert.Equal(expected, TitleSlug.Original(title));
    }

    [Fact]
    public void Original_IsCutAt120Characters()
    {
        Assert.Equal(TitleSlug.MaxOriginalLength, TitleSlug.Original(new string('a', 200)).Length);
    }

    // ---------- CA-8: campos de serie ----------

    [Fact]
    public void Load_RootFolderAndNumbering()
    {
        var config = new SeriesConfigLoader().Load(folder.Write("series.yaml", """
            series:
              - id: a
                nombre: A
                carpeta: "."
                numeracion: dependencia
              - id: b
                nombre: B
            """));

        Assert.Equal(Series.RootFolder, config.Find("a")!.ResolvedFolder);
        Assert.Equal(SeriesNumbering.Dependency, config.Find("a")!.Numbering);
        Assert.Equal(SeriesNumbering.Sequential, config.Find("b")!.Numbering);
    }

    [Fact]
    public void Load_InvalidNumbering_IsError()
    {
        var ex = Assert.Throws<SeriesConfigException>(() => new SeriesConfigLoader().Load(
            folder.Write("series.yaml", "series:\n  - id: a\n    nombre: A\n    numeracion: aleatoria\n")));

        Assert.Equal("Serie 'a', campo 'numeracion': 'aleatoria' no es válida; usa secuencial o dependencia.", Assert.Single(ex.Errors).ToString());
    }
}
