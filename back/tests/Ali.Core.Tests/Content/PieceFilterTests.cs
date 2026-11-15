using Ali.Core.Content;

namespace Ali.Core.Tests.Content;

public class PieceFilterTests
{
    private static readonly Piece Published = new()
    {
        Path = "casos/01.md",
        Series = "casos",
        Status = PieceStatus.Published,
        PlannedDate = new DateOnly(2026, 11, 9),
        PublishedDate = new DateOnly(2026, 11, 10),
    };

    private static readonly Piece Scheduled = new()
    {
        Path = "casos/02.md",
        Series = "casos",
        Status = PieceStatus.Scheduled,
        PlannedDate = new DateOnly(2026, 11, 17),
    };

    private static readonly Piece Ready = new()
    {
        Path = "resumen/01.md",
        Series = "resumen",
        Status = PieceStatus.Ready,
        PlannedDate = new DateOnly(2026, 11, 19),
    };

    private static readonly Piece Idea = new()
    {
        Path = "notas/ideas.md",
        Series = "notas",
        Status = PieceStatus.Idea,
    };

    private static readonly Piece Unclassified = new() { Path = "borradores/sin-metadatos.md" };

    private static readonly Piece[] All = [Idea, Unclassified, Ready, Scheduled, Published];

    private static IReadOnlyList<string> Paths(PieceQuery query) =>
        PieceFilter.Apply(All, query).Select(p => p.Path).ToList();

    // ---------- CA-1 y CA-4: sin filtros y orden ----------

    [Fact]
    public void Apply_EmptyQuery_ReturnsAllInChronologicalOrderUndatedLast()
    {
        Assert.Equal(
            ["casos/01.md", "casos/02.md", "resumen/01.md", "borradores/sin-metadatos.md", "notas/ideas.md"],
            Paths(new PieceQuery()));
    }

    [Fact]
    public void Apply_SameDate_TiesBrokenByPath()
    {
        var date = new DateOnly(2026, 11, 20);
        Piece[] pieces =
        [
            new() { Path = "b.md", PlannedDate = date },
            new() { Path = "A.md", PlannedDate = date },
            new() { Path = "a/z.md", PlannedDate = date },
        ];

        // Ordinal, case-insensitive: '.' (0x2E) sorts before '/' (0x2F).
        Assert.Equal(["A.md", "a/z.md", "b.md"], PieceFilter.Apply(pieces, new PieceQuery()).Select(p => p.Path));
    }

    [Fact]
    public void EffectiveDate_PrefersPublishedDate()
    {
        Assert.Equal(new DateOnly(2026, 11, 10), Published.EffectiveDate);
        Assert.Equal(new DateOnly(2026, 11, 17), Scheduled.EffectiveDate);
        Assert.Null(Idea.EffectiveDate);
    }

    // ---------- CA-2: filtros ----------

    [Fact]
    public void Apply_SeriesFilter_RepeatedValuesAreOr()
    {
        Assert.Equal(["casos/01.md", "casos/02.md"], Paths(new PieceQuery { Series = ["casos"] }));
        Assert.Equal(
            ["casos/01.md", "casos/02.md", "notas/ideas.md"],
            Paths(new PieceQuery { Series = ["casos", "notas"] }));
    }

    [Fact]
    public void Apply_UnknownSeries_ReturnsNothing()
    {
        Assert.Empty(Paths(new PieceQuery { Series = ["no-existe"] }));
    }

    [Fact]
    public void Apply_Unclassified_AloneOrCombinedWithSeries()
    {
        Assert.Equal(["borradores/sin-metadatos.md"], Paths(new PieceQuery { IncludeUnclassified = true }));
        Assert.Equal(
            ["resumen/01.md", "borradores/sin-metadatos.md"],
            Paths(new PieceQuery { Series = ["resumen"], IncludeUnclassified = true }));
    }

    [Fact]
    public void Apply_StatusFilter_RepeatedValuesAreOr()
    {
        Assert.Equal(["notas/ideas.md"], Paths(new PieceQuery { Statuses = [PieceStatus.Idea] }));
        Assert.Equal(
            ["casos/01.md", "resumen/01.md"],
            Paths(new PieceQuery { Statuses = [PieceStatus.Ready, PieceStatus.Published] }));
    }

    [Fact]
    public void Apply_DifferentFiltersAreAnd()
    {
        var query = new PieceQuery
        {
            Series = ["casos", "resumen"],
            Statuses = [PieceStatus.Ready, PieceStatus.Scheduled],
            To = new DateOnly(2026, 11, 18),
        };

        Assert.Equal(["casos/02.md"], Paths(query));
    }

    // ---------- CA-3: rango de fechas ----------

    [Fact]
    public void Apply_Range_IsInclusiveAndUsesEffectiveDate()
    {
        // Published: planned 11-09 but published 11-10, so 11-10 counts.
        var query = new PieceQuery { From = new DateOnly(2026, 11, 10), To = new DateOnly(2026, 11, 17) };

        Assert.Equal(["casos/01.md", "casos/02.md"], Paths(query));
    }

    [Fact]
    public void Apply_RangeExcludesPublishedPieceByItsPlannedDate()
    {
        Assert.Empty(Paths(new PieceQuery { From = new DateOnly(2026, 11, 9), To = new DateOnly(2026, 11, 9) }));
    }

    [Fact]
    public void Apply_OpenRange_FromOnlyOrToOnly_ExcludesUndated()
    {
        Assert.Equal(["resumen/01.md"], Paths(new PieceQuery { From = new DateOnly(2026, 11, 18) }));
        Assert.Equal(["casos/01.md"], Paths(new PieceQuery { To = new DateOnly(2026, 11, 10) }));
    }

    [Fact]
    public void Apply_FromAfterTo_Throws()
    {
        var query = new PieceQuery { From = new DateOnly(2026, 12, 1), To = new DateOnly(2026, 11, 1) };

        var ex = Assert.Throws<ArgumentException>(() => PieceFilter.Apply(All, query));
        Assert.StartsWith("La fecha 'desde' (2026-12-01) es posterior a 'hasta' (2026-11-01).", ex.Message);
    }
}
