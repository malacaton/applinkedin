using System.Globalization;
using System.Text;
using Ali.Core.Content;
using Ali.Core.Scheduling;

namespace Ali.Core.Tests.Content;

public class PieceCreatorTests : IDisposable
{
    // A Sunday; the next Tuesdays are 11-17, 11-24, 12-01.
    private static readonly DateOnly Sunday = D("2026-11-15");

    private static readonly FileNameTemplate DatedTemplate = Template("{fecha}-{titulo}");

    private static readonly SeriesConfig Config = new(
    [
        new Series("casos-reales", "Casos", new Cadence([DayOfWeek.Tuesday], CadenceFrequency.Weekly, null, null), null, "casos"),
        new Series("resumen-caso", "Resumen", null, new SeriesLink("casos-reales", 2)),
        new Series("fechada", "Fechada", null, null, "notas/fechadas", DatedTemplate),
        new Series("notas", "Notas", null, null, null, Template("{nnn}_{titulo}")),
    ]);

    private readonly TempContentFolder folder = new();
    private readonly PieceCreator creator = new();

    public void Dispose() => folder.Dispose();

    private static DateOnly D(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static FileNameTemplate Template(string text) =>
        FileNameTemplate.TryParse(text, out var template, out var error) ? template! : throw new FormatException(error);

    private IReadOnlyList<Piece> Catalog() => new CatalogReader().Read(folder.Root).Pieces;

    private CreatedPiece Create(NewPiece request) => creator.Create(folder.Root, Config, Catalog(), request);

    private static NewPiece Request(string series, string title) => new() { SeriesId = series, Title = title, SearchFrom = Sunday };

    private string ReadText(string path) => File.ReadAllText(Path.Combine(folder.Root, path), Encoding.UTF8);

    // ---------- CA-1: fichero y bloque ----------

    [Fact]
    public void Create_WritesFileInSeriesFolderWithMinimalBlock()
    {
        var created = Create(Request("casos-reales", "Qué aprendimos del RAG"));

        Assert.Equal("casos/01-que-aprendimos-del-rag.md", created.Path);
        Assert.Equal("---\nserie: casos-reales\nestado: idea\n---\n# Qué aprendimos del RAG\n", ReadText(created.Path));
        Assert.Equal("casos-reales", created.Piece.Series);
        Assert.Equal(PieceStatus.Idea, created.Piece.Status);
        Assert.Null(created.Slot);

        var reread = Assert.Single(Catalog());
        Assert.Equal(created.Path, reread.Path);
        Assert.Equal(PieceStatus.Idea, reread.Status);
    }

    [Fact]
    public void Create_DefaultFolderIsSeriesId_AndOptionalFieldsAreWritten()
    {
        var created = Create(Request("resumen-caso", " Resumen\r\n del caso ") with
        {
            PlannedDate = D("2026-11-19"),
            Format = "post",
            DependsOn = ["casos/01-agentes", " ", "casos/01-agentes.md"],
        });

        Assert.Equal("resumen-caso/01-resumen-del-caso.md", created.Path);
        Assert.Equal(
            "---\nserie: resumen-caso\nestado: idea\nfecha_prevista: 2026-11-19\nformato: post\ndepende_de:\n  - casos/01-agentes\n---\n# Resumen del caso\n",
            ReadText(created.Path));
    }

    [Fact]
    public void Create_QuotesValuesThatYamlWouldMisread()
    {
        var created = Create(Request("casos-reales", "Raro") with { Format = "true", DependsOn = ["#comentario", "a: b"] });

        Assert.Equal("true", created.Piece.Format);
        Assert.Equal(["#comentario", "a: b"], created.Piece.DependsOn);
        Assert.Contains("formato: \"true\"", ReadText(created.Path));
    }

    [Fact]
    public void Create_TemplateWithDate_UsesPlannedDate()
    {
        var created = Create(Request("fechada", "Nota") with { PlannedDate = D("2026-12-24") });

        Assert.Equal("notas/fechadas/2026-12-24-nota.md", created.Path);
    }

    // ---------- CA-2: numeración ----------

    [Fact]
    public void Create_NumberFollowsHighestPieceOfSeriesInAnyFolderAndStatus()
    {
        folder.Write("casos/01-uno.md", "---\nserie: casos-reales\nestado: publicado\n---\n");
        folder.Write("casos/07-siete.md", "---\nserie: casos-reales\nestado: idea\n---\n");
        folder.Write("archivo/12-doce.md", "---\nserie: casos-reales\nestado: borrador\n---\n");
        folder.Write("casos/sin-numero.md", "---\nserie: casos-reales\n---\n");
        folder.Write("casos/99-otra-serie.md", "---\nserie: notas\n---\n");

        var created = Create(Request("casos-reales", "Trece"));

        Assert.Equal("casos/13-trece.md", created.Path);
    }

    [Fact]
    public void Create_NumberPadding_FollowsTemplate()
    {
        folder.Write("notas/041_x.md", "---\nserie: notas\n---\n");

        Assert.Equal("notas/042_nueva.md", Create(Request("notas", "Nueva")).Path);
    }

    [Fact]
    public void Create_NumberLargerThanPadding_IsWrittenInFull()
    {
        folder.Write("casos/99-x.md", "---\nserie: casos-reales\n---\n");

        Assert.Equal("casos/100-cien.md", Create(Request("casos-reales", "Cien")).Path);
    }

    // ---------- CA-4: conflicto ----------

    [Fact]
    public void Create_ExistingFile_IsNotOverwritten()
    {
        // Not part of the series, so the numbering still gives 01.
        var existing = folder.Write("casos/01-repetida.md", "contenido previo");

        var ex = Assert.Throws<PieceCreationException>(() => Create(Request("casos-reales", "Repetida")));

        Assert.Equal("Ya existe 'casos/01-repetida.md'; no se sobrescribe.", ex.Message);
        Assert.Equal("contenido previo", File.ReadAllText(existing));
    }

    // ---------- CA-5: primera fecha libre ----------

    [Fact]
    public void Create_FirstFreeDate_UsesFirstSlot()
    {
        folder.Write("casos/01-uno.md", "---\nserie: casos-reales\nestado: programado\nfecha_prevista: 2026-11-17\n---\n");

        var created = Create(Request("casos-reales", "Dos") with { UseFirstFreeDate = true });

        Assert.Equal(D("2026-11-24"), created.Piece.PlannedDate);
        Assert.Equal(D("2026-11-24"), created.Slot!.Date);
        Assert.Contains("fecha_prevista: 2026-11-24\n", ReadText(created.Path));
    }

    [Fact]
    public void Create_FirstFreeDate_SkipsDatesOfIdeasAndDrafts()
    {
        var first = Create(Request("casos-reales", "Primera") with { UseFirstFreeDate = true });
        var second = Create(Request("casos-reales", "Segunda") with { UseFirstFreeDate = true });

        Assert.Equal(D("2026-11-17"), first.Piece.PlannedDate);
        Assert.Equal(D("2026-11-24"), second.Piece.PlannedDate);
        Assert.Equal("casos/02-segunda.md", second.Path);
    }

    [Fact]
    public void Create_FirstFreeDate_AfterSeries_AddsDependency()
    {
        folder.Write("casos/01-agentes.md", "---\nserie: casos-reales\nestado: publicado\nfecha_publicada: 2026-11-17\n---\n");

        var created = Create(Request("resumen-caso", "Resumen agentes") with { UseFirstFreeDate = true, DependsOn = ["otra/pieza"] });

        Assert.Equal(D("2026-11-19"), created.Piece.PlannedDate);
        Assert.Equal(["otra/pieza", "casos/01-agentes"], created.Piece.DependsOn);
        Assert.Equal("casos/01-agentes.md", created.Slot!.After!.Path);
    }

    [Fact]
    public void Create_FirstFreeDate_AfterSeries_DoesNotDuplicateDependency()
    {
        folder.Write("casos/01-agentes.md", "---\nserie: casos-reales\nestado: listo\nfecha_prevista: 2026-11-17\n---\n");

        var created = Create(Request("resumen-caso", "R") with { UseFirstFreeDate = true, DependsOn = ["Casos/01-Agentes.md"] });

        Assert.Equal(["Casos/01-Agentes.md"], created.Piece.DependsOn);
    }

    [Fact]
    public void Create_FirstFreeDate_NoSlots_Fails()
    {
        var ex = Assert.Throws<PieceCreationException>(() => Create(Request("resumen-caso", "R") with { UseFirstFreeDate = true }));

        Assert.Equal("No hay huecos libres para resumen-caso desde 2026-11-15.", ex.Message);
        Assert.Empty(Directory.GetFileSystemEntries(folder.Root));
    }

    [Fact]
    public void Create_FirstFreeDate_SeriesWithoutRule_Fails()
    {
        var ex = Assert.Throws<PieceCreationException>(() => Create(Request("notas", "N") with { UseFirstFreeDate = true }));

        Assert.Equal("La serie 'notas' no tiene cadencia; no se pueden calcular huecos.", ex.Message);
    }

    // ---------- Errores ----------

    [Theory]
    [InlineData("", "El título no contiene letras ni dígitos.")]
    [InlineData("¿?", "El título no contiene letras ni dígitos.")]
    public void Create_InvalidTitle_Fails(string title, string message)
    {
        var ex = Assert.Throws<PieceCreationException>(() => Create(Request("casos-reales", title)));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void Create_UnknownSeries_Fails()
    {
        var ex = Assert.Throws<PieceCreationException>(() => Create(Request("casos", "X")));

        Assert.StartsWith("La serie 'casos' no existe en el fichero de series.", ex.Message);
    }

    [Fact]
    public void Create_DateAndFirstFreeDate_Fails()
    {
        var ex = Assert.Throws<PieceCreationException>(
            () => Create(Request("casos-reales", "X") with { PlannedDate = Sunday, UseFirstFreeDate = true }));

        Assert.Equal("Usa --fecha o --fecha-libre, no las dos.", ex.Message);
    }

    [Fact]
    public void Create_DateTemplateWithoutDate_Fails()
    {
        var ex = Assert.Throws<PieceCreationException>(() => Create(Request("fechada", "X")));

        Assert.Equal("La serie 'fechada' usa {fecha} en el nombre: indica --fecha o --fecha-libre.", ex.Message);
        Assert.Empty(Directory.GetFileSystemEntries(folder.Root));
    }

    [Fact]
    public void Create_FolderOutsideRoot_Fails()
    {
        var config = new SeriesConfig([new Series("mala", "Mala", null, null, "../fuera")]);

        var ex = Assert.Throws<PieceCreationException>(
            () => creator.Create(folder.Root, config, [], Request("mala", "X")));

        Assert.Equal("La pieza debe estar dentro de la carpeta de contenido.", ex.Message);
    }

    [Fact]
    public void Create_MissingRoot_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => creator.Create(Path.Combine(folder.Root, "no-existe"), Config, [], Request("casos-reales", "X")));
    }
}
