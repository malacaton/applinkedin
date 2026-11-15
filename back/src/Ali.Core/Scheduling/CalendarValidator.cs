using System.Globalization;
using Ali.Core.Content;

namespace Ali.Core.Scheduling;

/// <summary>
/// Kinds of calendar problem, in report order (spec HU-06 §3).
/// </summary>
public enum ProblemKind
{
    Clash,
    Gap,
    OffCadence,
    Offset,
    Order,
    Blocked,
    BrokenDependency,
    UnknownSeries,
}

/// <summary>
/// Names used in the outputs for each <see cref="ProblemKind"/>.
/// </summary>
public static class ProblemKindNames
{
    public static string ToName(ProblemKind kind) => kind switch
    {
        ProblemKind.Clash => "choque",
        ProblemKind.Gap => "hueco",
        ProblemKind.OffCadence => "fuera_de_cadencia",
        ProblemKind.Offset => "desfase",
        ProblemKind.Order => "orden",
        ProblemKind.Blocked => "bloqueada",
        ProblemKind.BrokenDependency => "dependencia_rota",
        ProblemKind.UnknownSeries => "serie_desconocida",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

/// <param name="Kind">Type of problem.</param>
/// <param name="Date">Date of the problem, or null when it is not about a date.</param>
/// <param name="Series">Series of the problem (or of its main piece); null for unclassified pieces.</param>
/// <param name="Pieces">Paths of the pieces involved; the main one first.</param>
/// <param name="Message">Detail, in Spanish.</param>
public sealed record CalendarProblem(ProblemKind Kind, DateOnly? Date, string? Series, IReadOnlyList<string> Pieces, string Message)
{
    public bool Equals(CalendarProblem? other) =>
        other is not null
        && Kind == other.Kind
        && Date == other.Date
        && Series == other.Series
        && Pieces.SequenceEqual(other.Pieces)
        && Message == other.Message;

    public override int GetHashCode() => HashCode.Combine(Kind, Date, Series, Pieces.Count, Message);
}

/// <summary>
/// Problems found in the range [<paramref name="From"/>, <paramref name="To"/>], in report order.
/// </summary>
public sealed record CalendarReport(DateOnly From, DateOnly To, IReadOnlyList<CalendarProblem> Problems);

/// <summary>
/// The validation cannot be done: invalid range or unknown series in the filter.
/// </summary>
public sealed class CalendarValidationException(string message) : Exception(message);

/// <summary>
/// Checks the catalog against the series configuration. Pure and deterministic.
/// </summary>
public static class CalendarValidator
{
    /// <summary>
    /// Longest range allowed, in days between 'from' and 'to'.
    /// </summary>
    public const int MaxRangeDays = 366;

    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Validates <paramref name="pieces"/>. Date problems (clashes, gaps, off-cadence) are searched only in the range;
    /// dependency and series problems, in the whole catalog.
    /// </summary>
    /// <param name="series">Optional filter: keep only problems of these series.</param>
    /// <exception cref="CalendarValidationException">Invalid range or unknown series in <paramref name="series"/>.</exception>
    public static CalendarReport Validate(
        SeriesConfig config,
        IReadOnlyList<Piece> pieces,
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<string>? series = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(pieces);

        if (from > to)
        {
            throw new CalendarValidationException(
                $"La fecha 'desde' ({Format(from)}) es posterior a 'hasta' ({Format(to)}).");
        }

        if (to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            throw new CalendarValidationException($"El rango no puede superar un año ({MaxRangeDays} días).");
        }

        var filter = series?.Select(s => s.Trim()).Where(s => s.Length > 0).ToHashSet(StringComparer.Ordinal);
        foreach (var id in filter ?? [])
        {
            if (config.Find(id) is null)
            {
                throw new CalendarValidationException(SlotFinder.UnknownSeriesMessage(config, id));
            }
        }

        var ordered = SlotFinder.Ordered(pieces);
        var problems = new List<CalendarProblem>();

        AddClashes(problems, ordered, from, to);
        AddGaps(problems, config, ordered, from, to);
        AddOffCadence(problems, config, ordered, from, to);
        AddDependencyProblems(problems, config, ordered);
        AddUnknownSeries(problems, config, ordered);

        var result = problems
            .Where(p => filter is null || filter.Count == 0 || (p.Series is not null && filter.Contains(p.Series)))
            .OrderBy(p => p.Date is null ? 1 : 0)
            .ThenBy(p => p.Date)
            .ThenBy(p => p.Kind)
            .ThenBy(p => p.Pieces.FirstOrDefault() ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(p => p.Series ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(p => p.Message, StringComparer.Ordinal)
            .ToList();

        return new CalendarReport(from, to, result);
    }

    private static bool InRange(DateOnly? date, DateOnly from, DateOnly to) =>
        date is { } d && d >= from && d <= to;

    private static void AddClashes(List<CalendarProblem> problems, List<Piece> pieces, DateOnly from, DateOnly to)
    {
        var groups = pieces
            .Where(p => p.Series is not null && SlotFinder.Occupies(p) && InRange(p.EffectiveDate, from, to))
            .GroupBy(p => (Series: p.Series!, Date: p.EffectiveDate!.Value))
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            problems.Add(new CalendarProblem(
                ProblemKind.Clash,
                group.Key.Date,
                group.Key.Series,
                group.Select(p => p.Path).ToList(),
                $"{group.Count()} piezas de {group.Key.Series} el mismo día."));
        }
    }

    private static void AddGaps(List<CalendarProblem> problems, SeriesConfig config, List<Piece> pieces, DateOnly from, DateOnly to)
    {
        foreach (var series in config.Series.Where(SlotFinder.HasRule))
        {
            foreach (var slot in SlotFinder.Enumerate(series, pieces, from, to))
            {
                problems.Add(slot.After is { } after
                    ? new CalendarProblem(
                        ProblemKind.Gap,
                        slot.Date,
                        series.Id,
                        [after.Path],
                        $"Falta la pieza de {series.Id} que va {Days(series.After!.Days)} después de {after.Path}.")
                    : new CalendarProblem(ProblemKind.Gap, slot.Date, series.Id, [], "Toca publicar y no hay pieza."));
            }
        }
    }

    private static void AddOffCadence(List<CalendarProblem> problems, SeriesConfig config, List<Piece> pieces, DateOnly from, DateOnly to)
    {
        foreach (var piece in pieces.Where(p => p.Series is not null && SlotFinder.Occupies(p) && InRange(p.EffectiveDate, from, to)))
        {
            if (config.Find(piece.Series!) is { Cadence: { } cadence, After: null } series
                && !cadence.Matches(piece.EffectiveDate!.Value))
            {
                var day = SpanishDays.ToName(piece.EffectiveDate.Value.DayOfWeek);
                problems.Add(new CalendarProblem(
                    ProblemKind.OffCadence,
                    piece.EffectiveDate,
                    series.Id,
                    [piece.Path],
                    $"El {day} {Format(piece.EffectiveDate.Value)} no es día de publicación de {series.Id}."));
            }
        }
    }

    private static void AddDependencyProblems(List<CalendarProblem> problems, SeriesConfig config, List<Piece> pieces)
    {
        // First piece wins when two paths only differ in case.
        var index = new Dictionary<string, Piece>(StringComparer.OrdinalIgnoreCase);
        foreach (var piece in pieces)
        {
            index.TryAdd(PieceReference.Normalize(piece.Path), piece);
        }

        foreach (var piece in pieces)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in piece.DependsOn)
            {
                var key = PieceReference.Normalize(reference);
                if (!seen.Add(key))
                {
                    continue;
                }

                if (!index.TryGetValue(key, out var dependency))
                {
                    problems.Add(new CalendarProblem(
                        ProblemKind.BrokenDependency,
                        null,
                        piece.Series,
                        [piece.Path],
                        $"depende_de '{reference}' no existe."));
                    continue;
                }

                AddOrderOrOffset(problems, config, piece, dependency);

                if (!SlotFinder.IsReady(dependency.Status))
                {
                    var status = dependency.Status is { } s ? $"está en {PieceStatusNames.ToName(s)}" : "no tiene estado";
                    problems.Add(new CalendarProblem(
                        ProblemKind.Blocked,
                        piece.EffectiveDate,
                        piece.Series,
                        [piece.Path, dependency.Path],
                        $"Depende de {dependency.Path}, que {status}."));
                }
            }
        }
    }

    private static void AddOrderOrOffset(List<CalendarProblem> problems, SeriesConfig config, Piece piece, Piece dependency)
    {
        if (piece.EffectiveDate is not { } date || dependency.EffectiveDate is not { } dependencyDate)
        {
            return;
        }

        if (date < dependencyDate)
        {
            problems.Add(new CalendarProblem(
                ProblemKind.Order,
                date,
                piece.Series,
                [piece.Path, dependency.Path],
                $"Va el {Format(date)}, antes que {dependency.Path} ({Format(dependencyDate)})."));
            return;
        }

        if (piece.Series is not null
            && config.Find(piece.Series)?.After is { } link
            && link.SeriesId == dependency.Series
            && date.DayNumber - dependencyDate.DayNumber != link.Days)
        {
            problems.Add(new CalendarProblem(
                ProblemKind.Offset,
                date,
                piece.Series,
                [piece.Path, dependency.Path],
                $"Va {Days(date.DayNumber - dependencyDate.DayNumber)} después de {dependency.Path}; la serie pide {Days(link.Days)}."));
        }
    }

    private static void AddUnknownSeries(List<CalendarProblem> problems, SeriesConfig config, List<Piece> pieces)
    {
        foreach (var piece in pieces.Where(p => p.Series is not null && config.Find(p.Series) is null))
        {
            problems.Add(new CalendarProblem(
                ProblemKind.UnknownSeries,
                null,
                piece.Series,
                [piece.Path],
                $"La serie '{piece.Series}' no existe en el fichero de series."));
        }
    }

    private static string Days(int days) => days == 1 ? "1 día" : $"{days} días";

    private static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
}
