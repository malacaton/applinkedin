using System.Globalization;
using Ali.Core.Content;
using Ali.Core.Scheduling;

namespace Ali.Core.Tests.Scheduling;

public class CalendarValidatorTests
{
    // Range: Sunday 2026-11-15 to Monday 2026-11-30.
    // Cadence dates in range: casos-reales 11-17, 11-24; fundamentos 11-16, 11-19, 11-30.
    private static readonly DateOnly From = D("2026-11-15");
    private static readonly DateOnly To = D("2026-11-30");

    private static readonly SeriesConfig Config = new(
    [
        new Series("casos-reales", "Casos", new Cadence([DayOfWeek.Tuesday], CadenceFrequency.Weekly, null, null), null),
        new Series(
            "fundamentos",
            "Fundamentos",
            new Cadence([DayOfWeek.Monday, DayOfWeek.Thursday], CadenceFrequency.Biweekly, D("2026-11-02"), null),
            null),
        new Series("resumen-caso", "Resumen", null, new SeriesLink("casos-reales", 2)),
        new Series("notas", "Notas", null, null),
    ]);

    private static DateOnly D(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Piece P(string path, string? series, PieceStatus? status, string? date = null, params string[] dependsOn) => new()
    {
        Path = path,
        Series = series,
        Status = status,
        PlannedDate = date is null ? null : D(date),
        DependsOn = dependsOn,
        HasFrontMatter = true,
    };

    private static IReadOnlyList<CalendarProblem> Validate(params Piece[] pieces) =>
        CalendarValidator.Validate(Config, pieces, From, To).Problems;

    private static List<CalendarProblem> Of(ProblemKind kind, IEnumerable<CalendarProblem> problems) =>
        problems.Where(p => p.Kind == kind).ToList();

    // Pieces that cover every cadence date in range, so gap checks do not add noise.
    private static readonly Piece[] Covered =
    [
        P("casos/a.md", "casos-reales", PieceStatus.Published, "2026-11-17"),
        P("casos/b.md", "casos-reales", PieceStatus.Scheduled, "2026-11-24"),
        P("fundamentos/1.md", "fundamentos", PieceStatus.Ready, "2026-11-16"),
        P("fundamentos/2.md", "fundamentos", PieceStatus.Ready, "2026-11-19"),
        P("fundamentos/3.md", "fundamentos", PieceStatus.Ready, "2026-11-30"),
        P("resumenes/a.md", "resumen-caso", PieceStatus.Ready, "2026-11-19", "casos/a"),
        P("resumenes/b.md", "resumen-caso", PieceStatus.Ready, "2026-11-26", "casos/b"),
    ];

    [Fact]
    public void Validate_CoveredCalendar_HasNoProblems()
    {
        Assert.Empty(Validate(Covered));
    }

    // ---------- CA-1: choque ----------

    [Fact]
    public void Validate_TwoPiecesOfSameSeriesOnSameDate_Clash()
    {
        var problems = Validate(
        [
            .. Covered,
            P("casos/c.md", "casos-reales", PieceStatus.Ready, "2026-11-24"),
            // Off-cadence dates clash too.
            P("casos/d.md", "casos-reales", PieceStatus.Ready, "2026-11-18"),
            P("casos/e.md", "casos-reales", PieceStatus.Scheduled, "2026-11-18"),
        ]);

        var clashes = Of(ProblemKind.Clash, problems);
        Assert.Equal(2, clashes.Count);
        Assert.Equal(new CalendarProblem(
            ProblemKind.Clash, D("2026-11-18"), "casos-reales", ["casos/d.md", "casos/e.md"], "2 piezas de casos-reales el mismo día."),
            clashes[0]);
        Assert.Equal(["casos/b.md", "casos/c.md"], clashes[1].Pieces);
    }

    [Fact]
    public void Validate_DifferentSeriesOrDraftOnSameDate_DoNotClash()
    {
        var problems = Validate(
        [
            .. Covered,
            P("otra/x.md", "fundamentos", PieceStatus.Ready, "2026-11-17"),
            P("casos/borrador.md", "casos-reales", PieceStatus.Draft, "2026-11-17"),
            P("suelta.md", null, PieceStatus.Ready, "2026-11-17"),
            P("suelta2.md", null, PieceStatus.Ready, "2026-11-17"),
        ]);

        Assert.Empty(Of(ProblemKind.Clash, problems));
    }

    [Fact]
    public void Validate_ClashOutsideRange_IsNotReported()
    {
        var problems = Validate(
        [
            .. Covered,
            P("casos/x.md", "casos-reales", PieceStatus.Published, "2026-11-10"),
            P("casos/y.md", "casos-reales", PieceStatus.Published, "2026-11-10"),
        ]);

        Assert.Empty(Of(ProblemKind.Clash, problems));
    }

    // ---------- CA-2: hueco ----------

    [Fact]
    public void Validate_EmptyCatalog_ReportsEveryCadenceDateInRange()
    {
        var gaps = Validate();

        Assert.All(gaps, g => Assert.Equal(ProblemKind.Gap, g.Kind));
        Assert.Equal(
            [
                ("2026-11-16", "fundamentos"),
                ("2026-11-17", "casos-reales"),
                ("2026-11-19", "fundamentos"),
                ("2026-11-24", "casos-reales"),
                ("2026-11-30", "fundamentos"),
            ],
            gaps.Select(g => (g.Date!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), g.Series!)));
        Assert.All(gaps, g => Assert.Empty(g.Pieces));
        Assert.All(gaps, g => Assert.Equal("Toca publicar y no hay pieza.", g.Message));
    }

    [Fact]
    public void Validate_AfterSeries_ReportsUncoveredRelatedPieces()
    {
        var problems = Validate([.. Covered.Where(p => p.Path != "resumenes/b.md")]);

        var gap = Assert.Single(problems);
        Assert.Equal(new CalendarProblem(
            ProblemKind.Gap,
            D("2026-11-26"),
            "resumen-caso",
            ["casos/b.md"],
            "Falta la pieza de resumen-caso que va 2 días después de casos/b.md."),
            gap);
    }

    [Fact]
    public void Validate_GapAfterRange_IsNotReported()
    {
        // casos/c on 11-29 would need a summary on 12-01, after 'to'.
        var problems = Validate([.. Covered, P("casos/c.md", "casos-reales", PieceStatus.Ready, "2026-11-29")]);

        Assert.Empty(Of(ProblemKind.Gap, problems));
    }

    // ---------- CA-3: orden y desfase ----------

    [Fact]
    public void Validate_PieceBeforeItsDependency_IsOrderNotOffset()
    {
        var problems = Validate(
        [
            .. Covered.Where(p => p.Path != "resumenes/b.md"),
            P("resumenes/b.md", "resumen-caso", PieceStatus.Ready, "2026-11-20", "casos/b"),
        ]);

        var order = Assert.Single(Of(ProblemKind.Order, problems));
        Assert.Equal(new CalendarProblem(
            ProblemKind.Order,
            D("2026-11-20"),
            "resumen-caso",
            ["resumenes/b.md", "casos/b.md"],
            "Va el 2026-11-20, antes que casos/b.md (2026-11-24)."),
            order);
        Assert.Empty(Of(ProblemKind.Offset, problems));
    }

    [Fact]
    public void Validate_WrongDistanceForAfterSeries_IsOffset()
    {
        var problems = Validate(
        [
            .. Covered.Where(p => p.Path != "resumenes/b.md"),
            P("resumenes/b.md", "resumen-caso", PieceStatus.Ready, "2026-11-27", "casos/b.md"),
        ]);

        var offset = Assert.Single(problems);
        Assert.Equal(ProblemKind.Offset, offset.Kind);
        Assert.Equal("Va 3 días después de casos/b.md; la serie pide 2 días.", offset.Message);
        Assert.Equal(["resumenes/b.md", "casos/b.md"], offset.Pieces);
    }

    [Fact]
    public void Validate_DependencyWithoutAfterLink_HasNoOffset()
    {
        var problems = Validate([.. Covered, P("notas/x.md", "notas", PieceStatus.Ready, "2026-12-20", "casos/a")]);

        Assert.Empty(problems);
    }

    [Fact]
    public void Validate_DependencyProblems_IgnoreTheRange()
    {
        var problems = Validate(
        [
            .. Covered,
            P("casos/viejo.md", "casos-reales", PieceStatus.Published, "2026-10-06"),
            P("notas/x.md", "notas", PieceStatus.Published, "2026-10-01", "casos/viejo"),
        ]);

        Assert.Equal(D("2026-10-01"), Assert.Single(Of(ProblemKind.Order, problems)).Date);
    }

    // ---------- CA-4: bloqueada ----------

    [Theory]
    [InlineData(PieceStatus.Idea, "Depende de casos/x.md, que está en idea.")]
    [InlineData(PieceStatus.Draft, "Depende de casos/x.md, que está en borrador.")]
    [InlineData(null, "Depende de casos/x.md, que no tiene estado.")]
    public void Validate_DependencyNotReady_Blocks(PieceStatus? status, string message)
    {
        var problems = Validate([.. Covered, P("casos/x.md", "casos-reales", status), P("notas/y.md", "notas", PieceStatus.Draft, null, "casos/x")]);

        var blocked = Assert.Single(problems);
        Assert.Equal(new CalendarProblem(ProblemKind.Blocked, null, "notas", ["notas/y.md", "casos/x.md"], message), blocked);
    }

    [Theory]
    [InlineData(PieceStatus.Ready)]
    [InlineData(PieceStatus.Scheduled)]
    [InlineData(PieceStatus.Published)]
    public void Validate_DependencyReady_DoesNotBlock(PieceStatus status)
    {
        var problems = Validate([.. Covered, P("casos/x.md", "casos-reales", status), P("notas/y.md", "notas", PieceStatus.Draft, null, "casos/x")]);

        Assert.Empty(problems);
    }

    [Fact]
    public void Validate_BlockedPieceWithDate_UsesItsDate()
    {
        var problems = Validate([.. Covered, P("casos/x.md", "casos-reales", PieceStatus.Idea), P("notas/y.md", "notas", PieceStatus.Ready, "2026-12-24", "casos/x")]);

        Assert.Equal(D("2026-12-24"), Assert.Single(problems).Date);
    }

    // ---------- CA-5: dependencia rota y serie desconocida ----------

    [Fact]
    public void Validate_MissingDependency_IsBroken()
    {
        var problems = Validate([.. Covered, P("notas/y.md", "notas", PieceStatus.Idea, null, "casos/no-existe", "Casos\\A.MD")]);

        var broken = Assert.Single(problems);
        Assert.Equal(
            new CalendarProblem(ProblemKind.BrokenDependency, null, "notas", ["notas/y.md"], "depende_de 'casos/no-existe' no existe."),
            broken);
    }

    [Fact]
    public void Validate_RepeatedDependency_IsReportedOnce()
    {
        var problems = Validate([.. Covered, P("notas/y.md", "notas", PieceStatus.Idea, null, "casos/z", "casos/z.md")]);

        Assert.Single(problems);
    }

    [Fact]
    public void Validate_PieceWithUnknownSeries()
    {
        var problems = Validate([.. Covered, P("otros/x.md", "casos", PieceStatus.Idea)]);

        Assert.Equal(
            new CalendarProblem(ProblemKind.UnknownSeries, null, "casos", ["otros/x.md"], "La serie 'casos' no existe en el fichero de series."),
            Assert.Single(problems));
    }

    // ---------- CA-6: fuera de cadencia ----------

    [Fact]
    public void Validate_OccupyingPieceOnNonCadenceDay_IsOffCadence()
    {
        var problems = Validate(
        [
            .. Covered,
            P("casos/x.md", "casos-reales", PieceStatus.Ready, "2026-11-18"),
            P("casos/borrador.md", "casos-reales", PieceStatus.Draft, "2026-11-20"),
            P("casos/fuera.md", "casos-reales", PieceStatus.Ready, "2026-12-02"),
            // After series: the date is set by 'despues_de', never off-cadence.
            P("resumenes/z.md", "resumen-caso", PieceStatus.Ready, "2026-11-21"),
        ]);

        Assert.Equal(
            new CalendarProblem(
                ProblemKind.OffCadence,
                D("2026-11-18"),
                "casos-reales",
                ["casos/x.md"],
                "El miércoles 2026-11-18 no es día de publicación de casos-reales."),
            Assert.Single(Of(ProblemKind.OffCadence, problems)));
    }

    // ---------- CA-7: orden y determinismo ----------

    private static readonly Piece[] Mixed =
    [
        P("casos/a.md", "casos-reales", PieceStatus.Published, "2026-11-17"),
        P("casos/a2.md", "casos-reales", PieceStatus.Ready, "2026-11-17"),
        P("casos/borrador.md", "casos-reales", PieceStatus.Draft),
        P("resumenes/a.md", "resumen-caso", PieceStatus.Ready, "2026-11-16", "casos/a"),
        P("resumenes/r.md", "resumen-caso", PieceStatus.Idea, null, "casos/borrador", "casos/nada"),
        P("fundamentos/1.md", "fundamentos", PieceStatus.Ready, "2026-11-17"),
        P("raro.md", "inventada", PieceStatus.Idea),
    ];

    [Fact]
    public void Validate_SortsByDateThenKindThenPiece()
    {
        var problems = CalendarValidator.Validate(Config, Mixed, From, To).Problems;

        Assert.Equal(
            [
                "2026-11-16 hueco fundamentos",
                "2026-11-16 orden resumen-caso",
                "2026-11-17 choque casos-reales",
                "2026-11-17 fuera_de_cadencia fundamentos",
                "2026-11-19 hueco fundamentos",
                "2026-11-19 hueco resumen-caso",
                "2026-11-24 hueco casos-reales",
                "2026-11-30 hueco fundamentos",
                "— bloqueada resumen-caso",
                "— dependencia_rota resumen-caso",
                "— serie_desconocida inventada",
            ],
            problems.Select(p => $"{p.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—"} {ProblemKindNames.ToName(p.Kind)} {p.Series}"));
    }

    [Fact]
    public void Validate_SameInputInAnyOrder_GivesSameResult()
    {
        var expected = CalendarValidator.Validate(Config, Mixed, From, To).Problems;

        var random = new Random(7);
        for (var i = 0; i < 10; i++)
        {
            var shuffled = Mixed.OrderBy(_ => random.Next()).ToArray();
            Assert.Equal(expected, CalendarValidator.Validate(Config, shuffled, From, To).Problems);
        }
    }

    // ---------- Filtro y rango ----------

    [Fact]
    public void Validate_SeriesFilter_KeepsOnlyThoseSeries()
    {
        var problems = CalendarValidator.Validate(Config, Mixed, From, To, ["resumen-caso", " casos-reales "]).Problems;

        Assert.All(problems, p => Assert.Contains(p.Series, new[] { "resumen-caso", "casos-reales" }));
        Assert.Equal(6, problems.Count);
    }

    [Fact]
    public void Validate_UnknownSeriesInFilter_Throws()
    {
        var ex = Assert.Throws<CalendarValidationException>(() => CalendarValidator.Validate(Config, [], From, To, ["casos"]));

        Assert.StartsWith("La serie 'casos' no existe en el fichero de series. Series disponibles:", ex.Message);
    }

    [Fact]
    public void Validate_FromAfterTo_Throws()
    {
        var ex = Assert.Throws<CalendarValidationException>(() => CalendarValidator.Validate(Config, [], To, From));

        Assert.Equal("La fecha 'desde' (2026-11-30) es posterior a 'hasta' (2026-11-15).", ex.Message);
    }

    [Fact]
    public void Validate_RangeLimit()
    {
        CalendarValidator.Validate(Config, [], From, From.AddDays(366));

        var ex = Assert.Throws<CalendarValidationException>(() => CalendarValidator.Validate(Config, [], From, From.AddDays(367)));
        Assert.Equal("El rango no puede superar un año (366 días).", ex.Message);
    }

    [Fact]
    public void Validate_SingleDayRange_IsAllowed()
    {
        var problems = CalendarValidator.Validate(Config, [], D("2026-11-17"), D("2026-11-17")).Problems;

        Assert.Equal("casos-reales", Assert.Single(problems).Series);
    }
}
