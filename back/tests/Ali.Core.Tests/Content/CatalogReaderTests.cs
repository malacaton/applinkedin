using System.Text;
using Ali.Core.Content;

namespace Ali.Core.Tests.Content;

public class CatalogReaderTests : IDisposable
{
    private readonly TempContentFolder folder = new();
    private readonly CatalogReader reader = new();

    public void Dispose() => folder.Dispose();

    private Catalog Read() => reader.Read(folder.Root);

    private Piece Single(Catalog catalog, string path) => Assert.Single(catalog.Pieces, p => p.Path == path);

    // ---------- Errores de uso ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Read_BlankPath_Throws(string path)
    {
        Assert.Throws<ArgumentException>(() => reader.Read(path));
    }

    [Fact]
    public void Read_MissingFolder_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() => reader.Read(Path.Combine(folder.Root, "no-existe")));
    }

    [Fact]
    public void Read_EmptyFolder_ReturnsEmptyCatalog()
    {
        var catalog = Read();

        Assert.Empty(catalog.Pieces);
        Assert.Empty(catalog.Warnings);
    }

    // ---------- CA-1: recorrido ----------

    [Fact]
    public void Read_IncludesSubfoldersAndSkipsHiddenAndNonMarkdown()
    {
        folder.Write("raiz.md", "# Raíz");
        folder.Write("serie-a/01-uno.md", "# Uno");
        folder.Write("serie-a/sub/02-dos.MD", "# Dos");
        folder.Write("notas.txt", "no es markdown");
        folder.Write(".git/HEAD.md", "oculto");
        folder.Write(".obsidian/config.md", "oculto");
        folder.Write("serie-a/.borrador-oculto.md", "oculto");

        var catalog = Read();

        Assert.Equal(["raiz.md", "serie-a/01-uno.md", "serie-a/sub/02-dos.MD"], catalog.Pieces.Select(p => p.Path));
        Assert.Empty(catalog.Warnings);
    }

    // ---------- CA-2: campos ----------

    [Fact]
    public void Read_FrontMatter_ReadsAllFields()
    {
        folder.Write("pieza.md", """
            ---
            serie: casos-reales
            estado: programado
            fecha_prevista: 2026-11-17
            fecha_publicada: 2026-11-18
            url: https://www.linkedin.com/pulse/ejemplo
            depende_de:
              - articulos/01-intro
              - articulos/02-base.md
            formato: carrusel
            etiquetas: [ia, agentes]
            ---
            # Cuerpo
            """);

        var piece = Single(Read(), "pieza.md");

        Assert.True(piece.HasFrontMatter);
        Assert.False(piece.IsUnclassified);
        Assert.Equal("casos-reales", piece.Series);
        Assert.Equal(PieceStatus.Scheduled, piece.Status);
        Assert.Equal(new DateOnly(2026, 11, 17), piece.PlannedDate);
        Assert.Equal(new DateOnly(2026, 11, 18), piece.PublishedDate);
        Assert.Equal("https://www.linkedin.com/pulse/ejemplo", piece.Url);
        Assert.Equal(["articulos/01-intro", "articulos/02-base.md"], piece.DependsOn);
        Assert.Equal("carrusel", piece.Format);
    }

    [Fact]
    public void Read_DependsOnSingleValue_IsAList()
    {
        folder.Write("pieza.md", "---\nserie: s\ndepende_de: otra/pieza\n---\n");

        var piece = Single(Read(), "pieza.md");

        Assert.Equal(["otra/pieza"], piece.DependsOn);
    }

    [Theory]
    [InlineData("idea", PieceStatus.Idea)]
    [InlineData("borrador", PieceStatus.Draft)]
    [InlineData("listo", PieceStatus.Ready)]
    [InlineData("programado", PieceStatus.Scheduled)]
    [InlineData("publicado", PieceStatus.Published)]
    public void Read_ValidStatuses(string value, PieceStatus expected)
    {
        folder.Write("pieza.md", $"---\nestado: {value}\n---\n");

        Assert.Equal(expected, Single(Read(), "pieza.md").Status);
    }

    [Fact]
    public void Read_NullAndEmptyValues_AreAbsent()
    {
        folder.Write("pieza.md", "---\nserie: ~\nestado:\nurl: null\nformato: \"  \"\ndepende_de: []\n---\n");

        var catalog = Read();
        var piece = Single(catalog, "pieza.md");

        Assert.True(piece.HasFrontMatter);
        Assert.True(piece.IsUnclassified);
        Assert.Null(piece.Status);
        Assert.Null(piece.Url);
        Assert.Null(piece.Format);
        Assert.Empty(piece.DependsOn);
        Assert.Empty(catalog.Warnings);
    }

    [Fact]
    public void Read_EmptyFrontMatter_IsValidWithoutWarnings()
    {
        folder.Write("pieza.md", "---\n---\nCuerpo");

        var catalog = Read();
        var piece = Single(catalog, "pieza.md");

        Assert.True(piece.HasFrontMatter);
        Assert.True(piece.IsUnclassified);
        Assert.Empty(catalog.Warnings);
    }

    [Fact]
    public void Read_FrontMatterClosedWithDots_IsValid()
    {
        folder.Write("pieza.md", "---\nserie: s\n...\nCuerpo");

        Assert.Equal("s", Single(Read(), "pieza.md").Series);
    }

    // ---------- CA-3: sin bloque YAML ----------

    [Theory]
    [InlineData("# Título\n\nTexto sin metadatos.")]
    [InlineData("")]
    [InlineData("\n---\nserie: s\n---\n")]
    public void Read_NoFrontMatter_IsUnclassifiedWithoutWarnings(string content)
    {
        folder.Write("pieza.md", content);

        var catalog = Read();
        var piece = Single(catalog, "pieza.md");

        Assert.False(piece.HasFrontMatter);
        Assert.True(piece.IsUnclassified);
        Assert.Empty(catalog.Warnings);
    }

    // ---------- CA-4: bloque YAML defectuoso ----------

    [Theory]
    [InlineData("---\nserie: s\n# sin cierre\n", CatalogWarningCode.FrontMatterUnclosed)]
    [InlineData("---\nserie: [sin cerrar\n---\n", CatalogWarningCode.FrontMatterInvalid)]
    [InlineData("---\nserie: a\nserie: b\n---\n", CatalogWarningCode.FrontMatterInvalid)]
    [InlineData("---\n- uno\n- dos\n---\n", CatalogWarningCode.FrontMatterNotMapping)]
    [InlineData("---\nsolo un texto\n---\n", CatalogWarningCode.FrontMatterNotMapping)]
    public void Read_BrokenFrontMatter_KeepsPieceWarnsAndContinues(string content, CatalogWarningCode code)
    {
        folder.Write("a-valida.md", "---\nserie: s\n---\n");
        folder.Write("b-rota.md", content);
        folder.Write("c-valida.md", "---\nserie: s\n---\n");

        var catalog = Read();

        Assert.Equal(3, catalog.Pieces.Count);
        var broken = Single(catalog, "b-rota.md");
        Assert.False(broken.HasFrontMatter);
        Assert.True(broken.IsUnclassified);

        var warning = Assert.Single(catalog.Warnings);
        Assert.Equal("b-rota.md", warning.Path);
        Assert.Equal(code, warning.Code);
        Assert.False(string.IsNullOrWhiteSpace(warning.Message));
    }

    [Fact]
    public void Read_InvalidYaml_MessageIncludesFileLine()
    {
        // Line 1 is '---', so the broken line 'estado: [' is line 3 of the file.
        folder.Write("pieza.md", "---\nserie: s\nestado: [\n---\n");

        var warning = Assert.Single(Read().Warnings);

        Assert.Equal(CatalogWarningCode.FrontMatterInvalid, warning.Code);
        Assert.Contains("línea", warning.Message);
    }

    // ---------- CA-5: estado no válido ----------

    [Fact]
    public void Read_InvalidStatus_WarnsAndKeepsOtherFields()
    {
        folder.Write("pieza.md", "---\nserie: s\nestado: terminado\nfecha_prevista: 2026-12-01\n---\n");

        var catalog = Read();
        var piece = Single(catalog, "pieza.md");

        Assert.Null(piece.Status);
        Assert.Equal("s", piece.Series);
        Assert.Equal(new DateOnly(2026, 12, 1), piece.PlannedDate);

        var warning = Assert.Single(catalog.Warnings);
        Assert.Equal(CatalogWarningCode.InvalidStatus, warning.Code);
        Assert.Equal("estado", warning.Field);
        Assert.Contains("terminado", warning.Message);
    }

    // ---------- CA-6: fechas y tipos ----------

    [Theory]
    [InlineData("17/11/2026")]
    [InlineData("2026-02-30")]
    [InlineData("2026-11-17 09:00")]
    [InlineData("mañana")]
    public void Read_InvalidDate_WarnsAndKeepsPiece(string value)
    {
        folder.Write("pieza.md", $"---\nserie: s\nfecha_prevista: {value}\n---\n");

        var catalog = Read();
        var piece = Single(catalog, "pieza.md");

        Assert.Null(piece.PlannedDate);
        Assert.Equal("s", piece.Series);
        var warning = Assert.Single(catalog.Warnings);
        Assert.Equal(CatalogWarningCode.InvalidDate, warning.Code);
        Assert.Equal("fecha_prevista", warning.Field);
    }

    [Theory]
    [InlineData("serie: [a, b]", "serie")]
    [InlineData("url:\n  enlace: x", "url")]
    [InlineData("depende_de:\n  clave: valor", "depende_de")]
    [InlineData("depende_de:\n  - [anidada]", "depende_de")]
    public void Read_WrongFieldType_WarnsAndIgnoresField(string yaml, string field)
    {
        folder.Write("pieza.md", $"---\n{yaml}\nformato: post\n---\n");

        var catalog = Read();
        var piece = Single(catalog, "pieza.md");

        Assert.Equal("post", piece.Format);
        var warning = Assert.Single(catalog.Warnings);
        Assert.Equal(CatalogWarningCode.InvalidFieldType, warning.Code);
        Assert.Equal(field, warning.Field);
    }

    [Fact]
    public void Read_SeveralProblemsInOneFile_ReportsEach()
    {
        folder.Write("pieza.md", "---\nestado: hecho\nfecha_publicada: ayer\n---\n");

        var codes = Read().Warnings.Select(w => w.Code).ToList();

        Assert.Equal([CatalogWarningCode.InvalidStatus, CatalogWarningCode.InvalidDate], codes);
    }

    [Fact]
    public void Read_LockedFile_WarnsAndContinues()
    {
        var locked = folder.Write("b-bloqueada.md", "---\nserie: s\n---\n");
        folder.Write("a-libre.md", "---\nserie: s\n---\n");

        Catalog catalog;
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            catalog = Read();
        }

        Assert.Equal(2, catalog.Pieces.Count);
        Assert.Equal("s", Single(catalog, "a-libre.md").Series);
        var warning = Assert.Single(catalog.Warnings);
        Assert.Equal("b-bloqueada.md", warning.Path);
        Assert.Equal(CatalogWarningCode.FileUnreadable, warning.Code);
    }

    // ---------- CA-7: solo lectura ----------

    [Fact]
    public void Read_DoesNotModifyAnyFile()
    {
        folder.Write("a.md", "---\nserie: s\nestado: listo\n---\nCuerpo\r\n");
        folder.Write("b.md", "sin metadatos");
        folder.Write("c/rota.md", "---\nserie: [\n---\n");
        folder.Write("c/sin-cierre.md", "---\nserie: s\n");
        folder.Write("notas.txt", "otro");
        var before = Snapshot();

        Read();

        Assert.Equal(before, Snapshot());
    }

    private Dictionary<string, (string Content, DateTime Modified)> Snapshot() =>
        Directory.EnumerateFiles(folder.Root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                f => f,
                f => (Convert.ToBase64String(File.ReadAllBytes(f)), File.GetLastWriteTimeUtc(f)));

    // ---------- CA-8: determinismo ----------

    [Fact]
    public void Read_IsDeterministic()
    {
        folder.Write("z.md", "---\nserie: s\nestado: mal\n---\n");
        folder.Write("B/uno.md", "---\nserie: [\n---\n");
        folder.Write("a.md", "# a");
        folder.Write("b/dos.md", "---\nserie: s\n---\n");

        var first = Read();
        var second = Read();

        Assert.Equal(first.Pieces, second.Pieces, PieceComparer.Instance);
        Assert.Equal(first.Warnings, second.Warnings);
        Assert.Equal("a.md", first.Pieces[0].Path);
        Assert.Equal("z.md", first.Pieces[^1].Path);
    }

    // ---------- CA-9: codificación y formato ----------

    [Fact]
    public void Read_BomCrlfAndUppercaseStatus_AreInterpreted()
    {
        var text = "---  \r\nserie: s\r\nestado:  PUBLICADO \r\nfecha_publicada: 2026-11-10\r\n---\r\nCuerpo\r\n";
        folder.WriteBytes("pieza.md", [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)]);

        var catalog = Read();
        var piece = Single(catalog, "pieza.md");

        Assert.True(piece.HasFrontMatter);
        Assert.Equal("s", piece.Series);
        Assert.Equal(PieceStatus.Published, piece.Status);
        Assert.Equal(new DateOnly(2026, 11, 10), piece.PublishedDate);
        Assert.Empty(catalog.Warnings);
    }

    /// <summary>
    /// Records compare lists by reference; this compares DependsOn by content.
    /// </summary>
    private sealed class PieceComparer : IEqualityComparer<Piece>
    {
        public static readonly PieceComparer Instance = new();

        public bool Equals(Piece? x, Piece? y) =>
            x is not null && y is not null
            && (x with { DependsOn = [] }) == (y with { DependsOn = [] })
            && x.DependsOn.SequenceEqual(y.DependsOn);

        public int GetHashCode(Piece obj) => obj.Path.GetHashCode(StringComparison.Ordinal);
    }
}
