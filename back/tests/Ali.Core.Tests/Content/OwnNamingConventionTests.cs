using System.Globalization;
using Ali.Core.Content;
using Ali.Core.Scheduling;

namespace Ali.Core.Tests.Content;

/// <summary>
/// HU-11: own file name conventions (templates, numbering, root folder) and renaming when the date changes.
/// </summary>
public class OwnNamingConventionTests : IDisposable
{
    private const string SeriesYaml = """
        series:
          - id: principal
            nombre: Principal
            carpeta: "."
            fichero: "{nn} - Post {n} - {aammdd} - {titulo_original}"
            cadencia: { dias: martes, frecuencia: semanal, hora: "08:00" }
          - id: jueves
            nombre: Jueves
            carpeta: "."
            fichero: "{nn} - Post {n}j - {aammdd} - {titulo_original}"
            numeracion: dependencia
            cadencia: { dias: jueves, frecuencia: quincenal, desde: 2026-10-08 }
          - id: extraordinario
            nombre: Extraordinarios
            carpeta: extraordinarios
            fichero: "EXTRA {nn} - {aammdd} - {titulo_original}"
          - id: didactica
            nombre: Didáctica
            carpeta: serie-didactica
            fichero: "{nn} - {aammdd} - {titulo_original}"
        """;

    private readonly TempContentFolder folder = new();

    public OwnNamingConventionTests()
    {
        folder.Write("series.yaml", SeriesYaml);
        folder.Write("14 - Post 14 - 261117 - Primer post.md", "---\nserie: principal\nestado: listo\nfecha_prevista: 2026-11-17\n---\n# Primer\n");
        folder.Write("15 - Post 15 - 261124 - Segundo post.md", "---\nserie: principal\nestado: listo\nfecha_prevista: 2026-11-24\n---\n# Segundo\n");
        folder.Write("15 - Post 15 - 261124 - TIENE IMAGEN - Texto alternativo.txt", "");
        folder.Write("02B - Post 2B - 260820 - Variante.md", "---\nserie: principal\nestado: publicado\nfecha_publicada: 2026-08-20\n---\n");
        folder.Write("extraordinarios/EXTRA 07 - 261112 - Un extra.md", "---\nserie: extraordinario\nestado: publicado\nfecha_publicada: 2026-11-12\n---\n");
        folder.Write("serie-didactica/01 - 261112 - Qué es un LLM.md", "---\nserie: didactica\nestado: publicado\nfecha_publicada: 2026-11-12\n---\n");
        folder.Write("serie-didactica/01 - 261112 - Qué es un LLM.png", "png");
    }

    public void Dispose() => folder.Dispose();

    private static DateOnly D(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private SeriesConfig Config() => new SeriesConfigLoader().Load(Path.Combine(folder.Root, "series.yaml"));

    private IReadOnlyList<Piece> Pieces() => new CatalogReader().Read(folder.Root).Pieces;

    private CreatedPiece Create(NewPiece request) => new PieceCreator().Create(folder.Root, Config(), Pieces(), request);

    private static NewPiece Request(string series, string title) => new() { SeriesId = series, Title = title, SearchFrom = D("2026-11-15") };

    private PieceUpdateResult Update(string path, PieceChanges changes, SeriesConfig? config) =>
        new PieceUpdater().Update(folder.Root, path, changes, config);

    private string[] Files(string relativeFolder = "") =>
        Directory.GetFiles(Path.Combine(folder.Root, relativeFolder)).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;

    // ---------- CA-1: plantillas ----------

    [Fact]
    public void Create_InRootWithShortDateRepeatedNumberAndOriginalTitle()
    {
        var created = Create(Request("principal", "¿Por qué existe esta función?") with { PlannedDate = D("2026-12-01") });

        Assert.Equal("16 - Post 16 - 261201 - Por qué existe esta función.md", created.Path);
        Assert.True(File.Exists(Path.Combine(folder.Root, created.Path)));
        Assert.Equal("# ¿Por qué existe esta función?\n", File.ReadAllText(Path.Combine(folder.Root, created.Path)).Split("---\n")[2]);
    }

    [Fact]
    public void Create_ShortDateTemplateWithoutDate_Fails()
    {
        var ex = Assert.Throws<PieceCreationException>(() => Create(Request("principal", "Sin fecha")));

        Assert.Equal("La serie 'principal' usa {aammdd} en el nombre: indica --fecha o --fecha-libre.", ex.Message);
    }

    [Fact]
    public void Template_ReadsNumberAndReplacesDate()
    {
        Assert.True(FileNameTemplate.TryParse("{nn} - Post {n} - {aammdd} - {titulo_original}", out var template, out _));

        Assert.True(template!.TryMatch("15 - Post 15 - 261124 - Segundo post", out var match));
        Assert.Equal(15, match!.Number);
        Assert.Equal("15 - Post 15 - 261124".Length, match.DateEnd);
        Assert.Equal("15 - Post 15 - 261201 - Segundo post", template.ReplaceDate("15 - Post 15 - 261124 - Segundo post", D("2026-12-01")));

        Assert.False(template.TryMatch("15 - Post 14 - 261124 - Números distintos", out _));
        Assert.False(template.TryMatch("15 - Post 15j - 261126 - Un jueves", out _));
        Assert.False(template.TryMatch("15 - Post 15 - 261399 - Fecha imposible", out _));
        Assert.Null(template.ReplaceDate("No encaja", D("2026-12-01")));
    }

    // ---------- CA-3: numeración ----------

    [Fact]
    public void Create_NumberReadWithTemplate_NotOnlyLeadingDigits()
    {
        var created = Create(Request("extraordinario", "Otro extra") with { PlannedDate = D("2026-12-02") });

        Assert.Equal("extraordinarios/EXTRA 08 - 261202 - Otro extra.md", created.Path);
    }

    [Fact]
    public void Create_NamesThatDoNotFitTheTemplate_UseLeadingDigits()
    {
        folder.Write("40B - Post 40B - 270101 - Variante alta.md", "---\nserie: principal\nestado: idea\n---\n");

        var created = Create(Request("principal", "Siguiente") with { PlannedDate = D("2027-01-05") });

        Assert.StartsWith("41 - Post 41 - 270105", created.Path);
    }

    // ---------- CA-4: numeración por dependencia ----------

    [Fact]
    public void Create_DependencyNumbering_TakesNumberOfTheDependency()
    {
        var created = Create(Request("jueves", "Un jueves") with
        {
            PlannedDate = D("2026-11-19"),
            DependsOn = ["14 - Post 14 - 261117 - Primer post"],
        });

        Assert.Equal("14 - Post 14j - 261119 - Un jueves.md", created.Path);
        Assert.Equal(["14 - Post 14 - 261117 - Primer post"], created.Piece.DependsOn);
    }

    [Fact]
    public void Create_DependencyNumbering_WithoutDependency_Fails()
    {
        var ex = Assert.Throws<PieceCreationException>(() => Create(Request("jueves", "Sin dependencia") with { PlannedDate = D("2026-11-19") }));

        Assert.Equal("La serie 'jueves' numera por dependencia: indica --depende-de.", ex.Message);
    }

    [Fact]
    public void Create_DependencyNumbering_UnknownDependency_Fails()
    {
        var ex = Assert.Throws<PieceCreationException>(() => Create(Request("jueves", "X") with
        {
            PlannedDate = D("2026-11-19"),
            DependsOn = ["no-existe"],
        }));

        Assert.Equal("No se puede leer el número de 'no-existe', de la que depende la pieza.", ex.Message);
    }

    // ---------- CA-6: hora al crear ----------

    [Fact]
    public void Create_Time_DefaultsToSeriesCadence_OrUsesTheGivenOne()
    {
        var byDefault = Create(Request("principal", "Por defecto") with { PlannedDate = D("2026-12-01") });
        var explicitTime = Create(Request("principal", "A las cuatro") with { PlannedDate = D("2026-12-08"), Time = new TimeOnly(16, 0) });
        var noCadenceTime = Create(Request("extraordinario", "Sin hora") with { PlannedDate = D("2026-12-02") });

        Assert.Equal(new TimeOnly(8, 0), byDefault.Piece.Time);
        Assert.Contains("hora: \"08:00\"\n", File.ReadAllText(Path.Combine(folder.Root, byDefault.Path)));
        Assert.Equal(new TimeOnly(16, 0), explicitTime.Piece.Time);
        Assert.Null(noCadenceTime.Piece.Time);
    }

    // ---------- CA-7: renombrado ----------

    [Fact]
    public void Update_DateChange_RenamesPieceAndCompanions()
    {
        var result = Update("15 - Post 15 - 261124 - Segundo post.md", new PieceChanges { PlannedDate = D("2026-11-25") }, Config());

        Assert.Equal("15 - Post 15 - 261125 - Segundo post.md", result.Path);
        Assert.Equal(
            [
                new RenamedFile("15 - Post 15 - 261124 - Segundo post.md", "15 - Post 15 - 261125 - Segundo post.md"),
                new RenamedFile("15 - Post 15 - 261124 - TIENE IMAGEN - Texto alternativo.txt", "15 - Post 15 - 261125 - TIENE IMAGEN - Texto alternativo.txt"),
            ],
            result.Renamed);
        Assert.Null(result.RenameWarning);
        Assert.Contains("15 - Post 15 - 261125 - TIENE IMAGEN - Texto alternativo.txt", Files());
        Assert.DoesNotContain("15 - Post 15 - 261124 - Segundo post.md", Files());
        Assert.Contains("fecha_prevista: 2026-11-25", File.ReadAllText(Path.Combine(folder.Root, result.Path)));
    }

    [Fact]
    public void Update_PublishedDateNamesTheFile_InSubfolder()
    {
        var result = Update(
            "serie-didactica/01 - 261112 - Qué es un LLM.md",
            new PieceChanges { PublishedDate = D("2026-11-13") },
            Config());

        Assert.Equal(
            ["serie-didactica/01 - 261113 - Qué es un LLM.md", "serie-didactica/01 - 261113 - Qué es un LLM.png"],
            result.Renamed.Select(r => r.After));
        Assert.Equal(["01 - 261113 - Qué es un LLM.md", "01 - 261113 - Qué es un LLM.png"], Files("serie-didactica"));
    }

    [Fact]
    public void Update_TargetExists_RenamesNothing_KeepsEverything_AndWarns()
    {
        folder.Write("15 - Post 15 - 261125 - TIENE IMAGEN - Texto alternativo.txt", "otro");
        var before = Files();

        var result = Update("15 - Post 15 - 261124 - Segundo post.md", new PieceChanges { PlannedDate = D("2026-11-25") }, Config());

        Assert.Empty(result.Renamed);
        Assert.Equal("No se renombra: ya existe '15 - Post 15 - 261125 - TIENE IMAGEN - Texto alternativo.txt'.", result.RenameWarning);
        Assert.Equal(before, Files());
        Assert.Equal("otro", File.ReadAllText(Path.Combine(folder.Root, "15 - Post 15 - 261125 - TIENE IMAGEN - Texto alternativo.txt")));
        Assert.Contains("fecha_prevista: 2026-11-25", File.ReadAllText(Path.Combine(folder.Root, "15 - Post 15 - 261124 - Segundo post.md")));
    }

    [Fact]
    public void Update_NoRename_WithoutConfig_OrWhenTheDateDoesNotChange_OrNameDoesNotFit()
    {
        var withoutConfig = Update("15 - Post 15 - 261124 - Segundo post.md", new PieceChanges { PlannedDate = D("2026-11-25") }, null);
        var otherField = Update("14 - Post 14 - 261117 - Primer post.md", new PieceChanges { Status = PieceStatus.Scheduled }, Config());
        var notFitting = Update("02B - Post 2B - 260820 - Variante.md", new PieceChanges { PublishedDate = D("2026-08-21") }, Config());

        Assert.Empty(withoutConfig.Renamed);
        Assert.Empty(otherField.Renamed);
        Assert.Empty(notFitting.Renamed);
        Assert.Null(notFitting.RenameWarning);
        Assert.Contains("15 - Post 15 - 261124 - Segundo post.md", Files());
        Assert.Contains("02B - Post 2B - 260820 - Variante.md", Files());
    }

    // ---------- CA-6: hora al cambiar ----------

    [Fact]
    public void Update_Time_IsWrittenQuotedAndReadBack()
    {
        var result = Update("14 - Post 14 - 261117 - Primer post.md", new PieceChanges { Time = new TimeOnly(16, 0) }, Config());

        Assert.Equal([new FieldChange("hora", null, "16:00")], result.Changes);
        Assert.Contains("hora: \"16:00\"\n", File.ReadAllText(Path.Combine(folder.Root, "14 - Post 14 - 261117 - Primer post.md")));
        Assert.Equal(new TimeOnly(16, 0), Assert.Single(Pieces(), p => p.Path.StartsWith("14 - Post 14 ", StringComparison.Ordinal)).Time);

        var again = Update("14 - Post 14 - 261117 - Primer post.md", new PieceChanges { Time = new TimeOnly(16, 0) }, Config());
        Assert.False(again.HasChanges);
    }
}
