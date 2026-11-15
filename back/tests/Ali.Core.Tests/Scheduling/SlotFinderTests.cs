using System.Globalization;
using Ali.Core.Content;
using Ali.Core.Scheduling;

namespace Ali.Core.Tests.Scheduling;

public class SlotFinderTests
{
    // 2026-11-15 is a Sunday; 2026-11-17 a Tuesday.
    private static readonly DateOnly Sunday = D("2026-11-15");

    private static readonly Series Cases = new("casos-reales", "Casos reales", Weekly(DayOfWeek.Tuesday), null);
    private static readonly Series Basics = new(
        "fundamentos",
        "Fundamentos",
        new Cadence([DayOfWeek.Monday, DayOfWeek.Thursday], CadenceFrequency.Biweekly, D("2026-11-02"), null),
        null);
    private static readonly Series Summary = new("resumen-caso", "Resumen", null, new SeriesLink("casos-reales", 2));
    private static readonly Series Notes = new("notas", "Notas", null, null);

    private static readonly SeriesConfig Config = new([Cases, Basics, Summary, Notes]);

    private static DateOnly D(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Cadence Weekly(params DayOfWeek[] days) => new(days, CadenceFrequency.Weekly, null, null);

    private static Piece P(
        string path,
        string? series,
        PieceStatus? status,
        string? planned = null,
        string? published = null,
        params string[] dependsOn) => new()
        {
            Path = path,
            Series = series,
            Status = status,
            PlannedDate = planned is null ? null : D(planned),
            PublishedDate = published is null ? null : D(published),
            DependsOn = dependsOn,
            HasFrontMatter = true,
        };

    private static IEnumerable<DateOnly> Dates(SlotSearch search) => search.Slots.Select(s => s.Date);

    // ---------- CA-1: cadencia semanal ----------

    [Fact]
    public void Find_Weekly_ReturnsNextCadenceDates()
    {
        var search = SlotFinder.Find(Config, [], "casos-reales", Sunday, 3);

        Assert.Equal([D("2026-11-17"), D("2026-11-24"), D("2026-12-01")], Dates(search));
        Assert.Equal(Cases, search.Series);
        Assert.Equal(Sunday, search.From);
        Assert.All(search.Slots, s => Assert.Null(s.After));
        Assert.All(search.Slots, s => Assert.False(s.CadenceMismatch));
    }

    [Fact]
    public void Find_FromIsIncluded()
    {
        var search = SlotFinder.Find(Config, [], "casos-reales", D("2026-11-17"), 1);

        Assert.Equal([D("2026-11-17")], Dates(search));
    }

    [Fact]
    public void Find_TrimsSeriesId()
    {
        Assert.Single(SlotFinder.Find(Config, [], "  casos-reales ", Sunday, 1).Slots);
    }

    // ---------- CA-2: qué ocupa ----------

    [Fact]
    public void Find_OnlyReadyScheduledAndPublishedPiecesOfTheSeriesOccupy()
    {
        Piece[] pieces =
        [
            P("casos/01.md", "casos-reales", PieceStatus.Ready, "2026-11-17"),
            P("casos/02.md", "casos-reales", PieceStatus.Scheduled, "2026-11-24"),
            // Effective date is the published one, not the planned one.
            P("casos/03.md", "casos-reales", PieceStatus.Published, "2026-11-10", "2026-12-01"),
            P("casos/04.md", "casos-reales", PieceStatus.Idea, "2026-12-08"),
            P("casos/05.md", "casos-reales", PieceStatus.Draft, "2026-12-15"),
            P("casos/06.md", "casos-reales", PieceStatus.Ready),
            P("casos/07.md", "casos-reales", null, "2026-12-22"),
            P("otra/01.md", "fundamentos", PieceStatus.Ready, "2026-12-22"),
            P("suelta.md", null, PieceStatus.Ready, "2026-12-29"),
        ];

        var search = SlotFinder.Find(Config, pieces, "casos-reales", Sunday, 5);

        Assert.Equal(
            [D("2026-12-08"), D("2026-12-15"), D("2026-12-22"), D("2026-12-29"), D("2027-01-05")],
            Dates(search));
    }

    [Theory]
    [InlineData(PieceStatus.Ready, true)]
    [InlineData(PieceStatus.Scheduled, true)]
    [InlineData(PieceStatus.Published, true)]
    [InlineData(PieceStatus.Draft, false)]
    [InlineData(PieceStatus.Idea, false)]
    public void Occupies_DependsOnStatus(PieceStatus status, bool expected)
    {
        Assert.Equal(expected, SlotFinder.Occupies(P("x.md", "s", status, "2026-11-17")));
    }

    // ---------- CA-3: quincenal, varios días y cambio de año ----------

    [Fact]
    public void Find_BiweeklyAcrossYearChange()
    {
        // Publication weeks start on 2026-11-02, 11-16, 11-30, 12-14, 12-28, 2027-01-11...
        var search = SlotFinder.Find(Config, [], "fundamentos", D("2026-12-20"), 4);

        Assert.Equal([D("2026-12-28"), D("2026-12-31"), D("2027-01-11"), D("2027-01-14")], Dates(search));
    }

    [Fact]
    public void Find_SeveralDaysPerWeek_EachDayIsASlot()
    {
        var config = new SeriesConfig([new Series("dos", "Dos", Weekly(DayOfWeek.Tuesday, DayOfWeek.Thursday), null)]);
        Piece[] pieces = [P("dos/01.md", "dos", PieceStatus.Scheduled, "2026-11-19")];

        var search = SlotFinder.Find(config, pieces, "dos", Sunday, 3);

        Assert.Equal([D("2026-11-17"), D("2026-11-24"), D("2026-11-26")], Dates(search));
    }

    [Fact]
    public void Find_WeeklyAcrossYearChange()
    {
        var search = SlotFinder.Find(Config, [], "casos-reales", D("2026-12-28"), 2);

        Assert.Equal([D("2026-12-29"), D("2027-01-05")], Dates(search));
    }

    // ---------- CA-4: despues_de ----------

    private static readonly Piece[] LinkedPieces =
    [
        P("casos/a.md", "casos-reales", PieceStatus.Ready, "2026-11-17"),
        P("casos/b.md", "casos-reales", PieceStatus.Scheduled, "2026-11-24"),
        P("casos/c.md", "casos-reales", PieceStatus.Published, null, "2026-11-10"),
        P("casos/d.md", "casos-reales", PieceStatus.Idea, "2026-12-01"),
        P("casos/e.md", "casos-reales", PieceStatus.Ready, "2026-12-08"),
        P("resumenes/b.md", "resumen-caso", PieceStatus.Idea, null, null, "Casos\\B"),
        P("resumenes/e.md", "otra-serie", PieceStatus.Idea, null, null, "casos/e.md"),
    ];

    [Fact]
    public void Find_After_OneSlotPerUncoveredRelatedPiece()
    {
        var search = SlotFinder.Find(Config, LinkedPieces, "resumen-caso", Sunday, 5);

        // a → 11-19; b covered (any status, any spelling); c before 'from'; d is an idea;
        // e is cited only by a piece of another series, so it is not covered.
        Assert.Equal([D("2026-11-19"), D("2026-12-10")], Dates(search));
        Assert.Equal(["casos/a.md", "casos/e.md"], search.Slots.Select(s => s.After!.Path));
        Assert.All(search.Slots, s => Assert.False(s.CadenceMismatch));
    }

    [Fact]
    public void Find_After_RespectsCount()
    {
        var search = SlotFinder.Find(Config, LinkedPieces, "resumen-caso", Sunday, 1);

        Assert.Equal([D("2026-11-19")], Dates(search));
    }

    [Fact]
    public void Find_After_WithCadence_KeepsComputedDateAndFlagsMismatch()
    {
        // Two days after a Tuesday is a Thursday: matches 'jueves', not 'miércoles'.
        var config = new SeriesConfig(
        [
            Cases,
            new Series("en-jueves", "Jueves", Weekly(DayOfWeek.Thursday), new SeriesLink("casos-reales", 2)),
            new Series("en-miercoles", "Miércoles", Weekly(DayOfWeek.Wednesday), new SeriesLink("casos-reales", 2)),
        ]);
        Piece[] pieces = [P("casos/a.md", "casos-reales", PieceStatus.Ready, "2026-11-17")];

        var thursday = Assert.Single(SlotFinder.Find(config, pieces, "en-jueves", Sunday, 5).Slots);
        var wednesday = Assert.Single(SlotFinder.Find(config, pieces, "en-miercoles", Sunday, 5).Slots);

        Assert.Equal(D("2026-11-19"), thursday.Date);
        Assert.False(thursday.CadenceMismatch);
        Assert.Equal(D("2026-11-19"), wednesday.Date);
        Assert.True(wednesday.CadenceMismatch);
    }

    [Fact]
    public void Find_After_NothingPending_ReturnsEmpty()
    {
        Assert.Empty(SlotFinder.Find(Config, [], "resumen-caso", Sunday, 5).Slots);
    }

    // ---------- CA-5: errores ----------

    [Fact]
    public void Find_SeriesWithoutRule_Throws()
    {
        var ex = Assert.Throws<SlotSearchException>(() => SlotFinder.Find(Config, [], "notas", Sunday, 5));

        Assert.Equal("La serie 'notas' no tiene cadencia; no se pueden calcular huecos.", ex.Message);
    }

    [Fact]
    public void Find_UnknownSeries_ListsAvailableOnes()
    {
        var ex = Assert.Throws<SlotSearchException>(() => SlotFinder.Find(Config, [], "casos", Sunday, 5));

        Assert.Equal(
            "La serie 'casos' no existe en el fichero de series. Series disponibles: casos-reales, fundamentos, resumen-caso, notas.",
            ex.Message);
    }

    [Fact]
    public void Find_UnknownSeries_InEmptyConfig()
    {
        var ex = Assert.Throws<SlotSearchException>(() => SlotFinder.Find(new SeriesConfig([]), [], "casos", Sunday, 5));

        Assert.Equal("La serie 'casos' no existe: el fichero de series no define ninguna serie.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public void Find_CountOutOfRange_Throws(int count)
    {
        var ex = Assert.Throws<SlotSearchException>(() => SlotFinder.Find(Config, [], "casos-reales", Sunday, count));

        Assert.Equal("La cantidad debe ser un entero entre 1 y 100.", ex.Message);
    }

    [Fact]
    public void Find_MaxCount_IsAllowed()
    {
        Assert.Equal(100, SlotFinder.Find(Config, [], "casos-reales", Sunday, 100).Slots.Count);
    }

    // ---------- CA-6: coincidencias ----------

    [Fact]
    public void Find_SameDay_ListsOtherSeriesWithoutBlocking()
    {
        Piece[] pieces =
        [
            P("fundamentos/03.md", "fundamentos", PieceStatus.Scheduled, "2026-11-24"),
            P("fundamentos/02.md", "fundamentos", PieceStatus.Published, null, "2026-11-24"),
            P("fundamentos/04.md", "fundamentos", PieceStatus.Idea, "2026-11-24"),
            P("suelta.md", null, PieceStatus.Ready, "2026-11-24"),
        ];

        var search = SlotFinder.Find(Config, pieces, "casos-reales", Sunday, 2);

        Assert.Equal([D("2026-11-17"), D("2026-11-24")], Dates(search));
        Assert.Empty(search.Slots[0].SameDay);
        Assert.Equal(["fundamentos/02.md", "fundamentos/03.md"], search.Slots[1].SameDay.Select(p => p.Path));
    }

    // ---------- CA-7: determinismo ----------

    [Fact]
    public void Find_SameInputInAnyOrder_GivesSameResult()
    {
        Piece[] pieces =
        [
            .. LinkedPieces,
            P("fundamentos/x.md", "fundamentos", PieceStatus.Ready, "2026-11-19"),
            P("fundamentos/y.md", "fundamentos", PieceStatus.Ready, "2026-11-19"),
        ];
        var expected = Describe(SlotFinder.Find(Config, pieces, "resumen-caso", Sunday, 5));

        var random = new Random(42);
        for (var i = 0; i < 10; i++)
        {
            var shuffled = pieces.OrderBy(_ => random.Next()).ToArray();
            Assert.Equal(expected, Describe(SlotFinder.Find(Config, shuffled, "resumen-caso", Sunday, 5)));
        }
    }

    private static List<string> Describe(SlotSearch search) =>
        search.Slots
            .Select(s => $"{s.Date:yyyy-MM-dd}|{s.After?.Path}|{string.Join(",", s.SameDay.Select(p => p.Path))}|{s.CadenceMismatch}")
            .ToList();
}
