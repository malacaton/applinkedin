using System.Text;
using Ali.Core.Content;

namespace Ali.Core.Tests.Content;

public class PieceUpdaterTests : IDisposable
{
    private static readonly DateTime OldTimestamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    private readonly TempContentFolder folder = new();
    private readonly PieceUpdater updater = new();

    public void Dispose() => folder.Dispose();

    private PieceUpdateResult Update(string path, PieceChanges changes) => updater.Update(folder.Root, path, changes);

    private Piece Reread(string path) => Assert.Single(new CatalogReader().Read(folder.Root).Pieces, p => p.Path == path);

    private string ReadText(string path) => File.ReadAllText(Path.Combine(folder.Root, path), Encoding.UTF8);

    private byte[] ReadBytes(string path) => File.ReadAllBytes(Path.Combine(folder.Root, path));

    private static DateOnly Date(string value) => DateOnly.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    // ---------- CA-1: campos ----------

    [Fact]
    public void Update_Status_ReplacesLineAndRereadsNewValue()
    {
        folder.Write("casos/02.md", "---\nserie: casos\nestado: listo\nfecha_prevista: 2026-11-17\n---\n# Memoria\n");

        var result = Update("casos/02.md", new PieceChanges { Status = PieceStatus.Scheduled });

        Assert.Equal("---\nserie: casos\nestado: programado\nfecha_prevista: 2026-11-17\n---\n# Memoria\n", ReadText("casos/02.md"));
        Assert.Equal(PieceStatus.Scheduled, Reread("casos/02.md").Status);
        Assert.Equal([new FieldChange("estado", "listo", "programado")], result.Changes);
        Assert.Equal(PieceStatus.Ready, result.Before.Status);
        Assert.Equal(PieceStatus.Scheduled, result.After.Status);
        Assert.Equal("casos/02.md", result.Path);
    }

    [Theory]
    [InlineData("fecha_prevista")]
    [InlineData("fecha_publicada")]
    [InlineData("url")]
    public void Update_SingleField_ChangesOnlyThatField(string field)
    {
        folder.Write("p.md", "---\nserie: s\nestado: listo\nfecha_prevista: 2026-11-01\nfecha_publicada: 2026-11-02\nurl: https://a.example/1\n---\n");
        var changes = field switch
        {
            "fecha_prevista" => new PieceChanges { PlannedDate = Date("2026-12-01") },
            "fecha_publicada" => new PieceChanges { PublishedDate = Date("2026-12-02") },
            _ => new PieceChanges { Url = "https://b.example/2" },
        };

        Update("p.md", changes);

        var piece = Reread("p.md");
        Assert.Equal("s", piece.Series);
        Assert.Equal(PieceStatus.Ready, piece.Status);
        Assert.Equal(field == "fecha_prevista" ? Date("2026-12-01") : Date("2026-11-01"), piece.PlannedDate);
        Assert.Equal(field == "fecha_publicada" ? Date("2026-12-02") : Date("2026-11-02"), piece.PublishedDate);
        Assert.Equal(field == "url" ? "https://b.example/2" : "https://a.example/1", piece.Url);
    }

    [Fact]
    public void Update_AllFieldsTogether_AddsMissingOnesBeforeClosingInFixedOrder()
    {
        folder.Write("p.md", "---\nserie: s\n---\nCuerpo");

        var result = Update("p.md", new PieceChanges
        {
            Url = "https://www.linkedin.com/pulse/x",
            PublishedDate = Date("2026-11-18"),
            PlannedDate = Date("2026-11-17"),
            Status = PieceStatus.Published,
        });

        Assert.Equal(
            "---\nserie: s\nestado: publicado\nfecha_prevista: 2026-11-17\nfecha_publicada: 2026-11-18\nurl: https://www.linkedin.com/pulse/x\n---\nCuerpo",
            ReadText("p.md"));
        Assert.Equal(["estado", "fecha_prevista", "fecha_publicada", "url"], result.Changes.Select(c => c.Field));
        Assert.All(result.Changes, c => Assert.Null(c.OldValue));
    }

    [Theory]
    [InlineData("casos/02")]
    [InlineData("casos\\02.md")]
    [InlineData("  casos/02  ")]
    [InlineData("./casos//02.md")]
    public void Update_AcceptsPathVariants(string path)
    {
        folder.Write("casos/02.md", "---\nestado: idea\n---\n");

        var result = Update(path, new PieceChanges { Status = PieceStatus.Draft });

        Assert.True(result.HasChanges);
        Assert.Equal(PieceStatus.Draft, Reread("casos/02.md").Status);
    }

    [Fact]
    public void Update_CaseInsensitiveFileSystem_KeepsRealFileName()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Solo en sistemas de ficheros sin distinción de mayúsculas.");
        folder.Write("Casos/02-Memoria.md", "---\nestado: idea\n---\n");

        var result = Update("casos/02-MEMORIA.MD", new PieceChanges { Status = PieceStatus.Draft });

        Assert.Equal("Casos/02-Memoria.md", result.Path);
        Assert.Equal(["02-Memoria.md"], Directory.GetFiles(Path.Combine(folder.Root, "Casos")).Select(Path.GetFileName));
    }

    [Fact]
    public void Update_AbsolutePathInsideRoot_IsAccepted()
    {
        var full = folder.Write("casos/02.md", "---\nestado: idea\n---\n");

        var result = Update(full, new PieceChanges { Status = PieceStatus.Draft });

        Assert.Equal("casos/02.md", result.Path);
    }

    [Fact]
    public void Update_LeavesNoTemporaryFiles()
    {
        folder.Write("casos/02.md", "---\nestado: idea\n---\n");

        Update("casos/02.md", new PieceChanges { Status = PieceStatus.Draft });

        Assert.Equal(["02.md"], Directory.GetFiles(Path.Combine(folder.Root, "casos")).Select(Path.GetFileName));
    }

    // ---------- CA-2: cuerpo byte a byte ----------

    [Theory]
    [InlineData(false, "\n")]
    [InlineData(true, "\n")]
    [InlineData(false, "\r\n")]
    [InlineData(true, "\r\n")]
    public void Update_KeepsBodyBomAndLineBreaks(bool withBom, string newLine)
    {
        var header = Encoding.UTF8.GetBytes($"---{newLine}serie: s{newLine}estado: borrador{newLine}---{newLine}");
        // Mixed line breaks, trailing spaces and a byte that is not valid UTF-8: copied as is.
        byte[] body = [.. Encoding.UTF8.GetBytes($"# Título  {newLine}\r\nlínea\n\n---\nestado: idea\n"), 0xFF, 0x00, (byte)'x'];
        folder.WriteBytes("p.md", [.. withBom ? Bom : [], .. header, .. body]);

        Update("p.md", new PieceChanges { Status = PieceStatus.Ready, Url = "https://a.example/x" });

        var bytes = ReadBytes("p.md");
        var expectedHeader = Encoding.UTF8.GetBytes(
            $"---{newLine}serie: s{newLine}estado: listo{newLine}url: https://a.example/x{newLine}---{newLine}");
        Assert.Equal([.. withBom ? Bom : [], .. expectedHeader, .. body], bytes);
    }

    // ---------- CA-3: resto del bloque ----------

    [Fact]
    public void Update_KeepsOtherFieldsCommentsUnknownFieldsAndOrder()
    {
        const string original = """
            ---
            # Cabecera del bloque
            formato: articulo
            estado: borrador  # revisar
            campo_propio: "valor: con dos puntos"

            depende_de:
              - casos/01
              - casos/00
            serie: casos
            url: >
              https://antigua.example/
              continuacion

            fecha_prevista: 2026-11-17
            ...
            Cuerpo
            """;
        folder.Write("p.md", original);

        Update("p.md", new PieceChanges { Status = PieceStatus.Ready, Url = "https://nueva.example/x" });

        const string expected = """
            ---
            # Cabecera del bloque
            formato: articulo
            estado: listo
            campo_propio: "valor: con dos puntos"

            depende_de:
              - casos/01
              - casos/00
            serie: casos
            url: https://nueva.example/x

            fecha_prevista: 2026-11-17
            ...
            Cuerpo
            """;
        Assert.Equal(expected, ReadText("p.md"));

        var piece = Reread("p.md");
        Assert.Equal(["casos/01", "casos/00"], piece.DependsOn);
        Assert.Equal("articulo", piece.Format);
    }

    [Fact]
    public void Update_DoesNotMatchKeysThatOnlyStartWithTheFieldName()
    {
        folder.Write("p.md", "---\nestado_anterior: idea\nurl_borrador: https://a.example/\n---\n");

        Update("p.md", new PieceChanges { Status = PieceStatus.Draft, Url = "https://b.example/" });

        Assert.Equal(
            "---\nestado_anterior: idea\nurl_borrador: https://a.example/\nestado: borrador\nurl: https://b.example/\n---\n",
            ReadText("p.md"));
    }

    [Fact]
    public void Update_ReplacesNullAndInvalidValues()
    {
        folder.Write("p.md", "---\nestado: hecho\nfecha_prevista:\nfecha_publicada: ~\n---\n");

        var result = Update("p.md", new PieceChanges
        {
            Status = PieceStatus.Idea,
            PlannedDate = Date("2026-11-20"),
            PublishedDate = Date("2026-11-21"),
        });

        Assert.Equal("---\nestado: idea\nfecha_prevista: 2026-11-20\nfecha_publicada: 2026-11-21\n---\n", ReadText("p.md"));
        Assert.Equal(new FieldChange("estado", null, "idea"), result.Changes[0]);
    }

    // ---------- CA-4: sin bloque ----------

    [Fact]
    public void Update_WithoutFrontMatter_CreatesBlockAtStart()
    {
        folder.Write("p.md", "# Sin metadatos\n\nTexto.\n");

        var result = Update("p.md", new PieceChanges { Status = PieceStatus.Ready });

        Assert.Equal("---\nestado: listo\n---\n# Sin metadatos\n\nTexto.\n", ReadText("p.md"));
        Assert.True(result.After.HasFrontMatter);
        Assert.True(Reread("p.md").IsUnclassified);
    }

    [Fact]
    public void Update_WithoutFrontMatter_KeepsBomFirstAndUsesFileLineBreak()
    {
        byte[] body = [.. Encoding.UTF8.GetBytes("# Título\r\nTexto")];
        folder.WriteBytes("p.md", [.. Bom, .. body]);

        Update("p.md", new PieceChanges { Status = PieceStatus.Idea });

        Assert.Equal([.. Bom, .. Encoding.UTF8.GetBytes("---\r\nestado: idea\r\n---\r\n"), .. body], ReadBytes("p.md"));
    }

    [Fact]
    public void Update_EmptyFileAndEmptyBlock_GetTheFields()
    {
        folder.Write("vacio.md", "");
        folder.Write("bloque.md", "---\n---\n");

        Update("vacio.md", new PieceChanges { Status = PieceStatus.Idea });
        Update("bloque.md", new PieceChanges { Status = PieceStatus.Idea });

        Assert.Equal("---\nestado: idea\n---\n", ReadText("vacio.md"));
        Assert.Equal("---\nestado: idea\n---\n", ReadText("bloque.md"));
    }

    // ---------- CA-5: publicado ----------

    [Fact]
    public void Update_PublishedWithoutAnyPublishedDate_IsRejected()
    {
        folder.Write("p.md", "---\nestado: programado\n---\n");

        var ex = Assert.Throws<PieceUpdateException>(() => Update("p.md", new PieceChanges { Status = PieceStatus.Published }));

        Assert.Equal("Para marcar como publicado hace falta --fecha-publicada (la pieza no tiene fecha_publicada).", ex.Message);
    }

    [Fact]
    public void Update_PublishedWithExistingPublishedDate_IsAccepted()
    {
        folder.Write("p.md", "---\nestado: programado\nfecha_publicada: 2026-11-18\n---\n");

        Update("p.md", new PieceChanges { Status = PieceStatus.Published });

        Assert.Equal(PieceStatus.Published, Reread("p.md").Status);
    }

    [Fact]
    public void Update_PublishedWithNewPublishedDate_IsAccepted()
    {
        folder.Write("p.md", "---\nestado: programado\n---\n");

        Update("p.md", new PieceChanges { Status = PieceStatus.Published, PublishedDate = Date("2026-11-18") });

        var piece = Reread("p.md");
        Assert.Equal(PieceStatus.Published, piece.Status);
        Assert.Equal(Date("2026-11-18"), piece.PublishedDate);
    }

    // ---------- CA-6: rechazos sin modificar ----------

    public static TheoryData<string, string, string> RejectedCases => new()
    {
        { "---\nserie: [\n---\nCuerpo", "url", "El bloque YAML de 'p.md' no es válido (YAML no válido en la línea" },
        { "---\nserie: s\nCuerpo sin cierre", "estado", "El bloque YAML de 'p.md' no es válido (empieza con '---' pero no tiene línea de cierre); corrígelo a mano antes de cambiarlo." },
        { "---\n- a\n- b\n---\n", "estado", "El bloque YAML de 'p.md' no es válido (El bloque YAML debe ser una lista de pares clave: valor); corrígelo a mano antes de cambiarlo." },
        { "---\nestado: idea\nestado: listo\n---\n", "estado", "El bloque YAML de 'p.md' no es válido (YAML no válido" },
        { "---\nestado: idea\n---\n", "ftp", "URL 'ftp://a.example/x' no válida: debe empezar por http:// o https://." },
        { "---\nestado: idea\n---\n", "relativa", "URL 'linkedin.com/x' no válida: debe empezar por http:// o https://." },
        { "---\nestado: idea\n---\n", "espacios", "URL 'https://a.example/x y' no válida: debe empezar por http:// o https://." },
    };

    [Theory]
    [MemberData(nameof(RejectedCases))]
    public void Update_Rejected_LeavesFileUntouched(string content, string kind, string messageStart)
    {
        var full = folder.Write("p.md", content);
        File.SetLastWriteTimeUtc(full, OldTimestamp);
        var changes = kind switch
        {
            "url" => new PieceChanges { Url = "https://a.example/x" },
            "ftp" => new PieceChanges { Url = "ftp://a.example/x" },
            "relativa" => new PieceChanges { Url = "linkedin.com/x" },
            "espacios" => new PieceChanges { Url = "https://a.example/x y" },
            _ => new PieceChanges { Status = PieceStatus.Ready },
        };

        var ex = Assert.Throws<PieceUpdateException>(() => Update("p.md", changes));

        Assert.StartsWith(messageStart, ex.Message);
        Assert.Equal(Encoding.UTF8.GetBytes(content), ReadBytes("p.md"));
        Assert.Equal(OldTimestamp, File.GetLastWriteTimeUtc(full));
        Assert.Single(Directory.GetFiles(folder.Root));
    }

    [Theory]
    [InlineData("../fuera.md")]
    [InlineData("casos/../../fuera.md")]
    public void Update_PathWithParentSegments_IsRejected(string path)
    {
        folder.Write("casos/02.md", "---\nestado: idea\n---\n");

        var ex = Assert.Throws<PieceUpdateException>(() => Update(path, new PieceChanges { Status = PieceStatus.Ready }));

        Assert.Equal("La pieza debe estar dentro de la carpeta de contenido.", ex.Message);
    }

    [Fact]
    public void Update_AbsolutePathOutsideRoot_IsRejected()
    {
        using var other = new TempContentFolder();
        var outside = other.Write("fuera.md", "---\nestado: idea\n---\n");

        var ex = Assert.Throws<PieceUpdateException>(() => Update(outside, new PieceChanges { Status = PieceStatus.Ready }));

        Assert.Equal("La pieza debe estar dentro de la carpeta de contenido.", ex.Message);
        Assert.Equal("---\nestado: idea\n---\n", File.ReadAllText(outside));
    }

    [Theory]
    [InlineData("casos/no-existe.md", "casos/no-existe.md")]
    [InlineData("notas.txt", "notas.txt.md")]
    [InlineData(".oculta/p.md", ".oculta/p.md")]
    public void Update_MissingOrHiddenPiece_IsRejected(string path, string shown)
    {
        folder.Write("notas.txt", "texto");
        folder.Write(".oculta/p.md", "---\nestado: idea\n---\n");

        var ex = Assert.Throws<PieceUpdateException>(() => Update(path, new PieceChanges { Status = PieceStatus.Ready }));

        Assert.Equal($"No existe la pieza '{shown}' en {Path.TrimEndingDirectorySeparator(folder.Root)}.", ex.Message);
    }

    [Fact]
    public void Update_NoFields_IsRejected()
    {
        folder.Write("p.md", "---\nestado: idea\n---\n");

        var ex = Assert.Throws<PieceUpdateException>(() => Update("p.md", new PieceChanges()));

        Assert.StartsWith("Indica al menos un campo que cambiar", ex.Message);
    }

    [Fact]
    public void Update_MissingRoot_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => updater.Update(Path.Combine(folder.Root, "no-existe"), "p.md", new PieceChanges { Status = PieceStatus.Idea }));
    }

    [Fact]
    public void Update_FlowMappingThatCannotBeEditedByLines_IsRejectedBySafetyCheck()
    {
        const string content = "---\n{serie: s, estado: idea}\n---\n";
        folder.Write("p.md", content);

        var ex = Assert.Throws<PieceUpdateException>(() => Update("p.md", new PieceChanges { Status = PieceStatus.Ready }));

        Assert.StartsWith("No se pudo actualizar 'p.md' de forma segura", ex.Message);
        Assert.Equal(content, ReadText("p.md"));
    }

    // ---------- CA-7: sin cambios ----------

    [Fact]
    public void Update_SameValues_DoesNotWrite()
    {
        var full = folder.Write("p.md", "---\nestado: Publicado\nfecha_publicada: 2026-11-18\nurl: https://a.example/x\n---\n");
        File.SetLastWriteTimeUtc(full, OldTimestamp);

        var result = Update("p.md", new PieceChanges
        {
            Status = PieceStatus.Published,
            PublishedDate = Date("2026-11-18"),
            Url = "https://a.example/x",
        });

        Assert.False(result.HasChanges);
        Assert.Equal(result.Before, result.After);
        Assert.Equal(OldTimestamp, File.GetLastWriteTimeUtc(full));
        Assert.Equal("---\nestado: Publicado\nfecha_publicada: 2026-11-18\nurl: https://a.example/x\n---\n", ReadText("p.md"));
    }
}
