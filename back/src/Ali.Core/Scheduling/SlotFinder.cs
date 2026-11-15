using Ali.Core.Content;

namespace Ali.Core.Scheduling;

/// <summary>
/// A free date for a series.
/// </summary>
/// <param name="Date">Proposed publication date.</param>
/// <param name="After">For series with 'despues_de': the related piece this slot follows; otherwise null.</param>
/// <param name="SameDay">Pieces of other series on the same date. Informative only: they do not block the slot.</param>
/// <param name="CadenceMismatch">True when the series has a cadence and <paramref name="Date"/> does not match it.</param>
public sealed record Slot(DateOnly Date, Piece? After, IReadOnlyList<Piece> SameDay, bool CadenceMismatch);

/// <summary>
/// Result of a slot search.
/// </summary>
public sealed record SlotSearch(Series Series, DateOnly From, IReadOnlyList<Slot> Slots);

/// <summary>
/// The search cannot be done: unknown series, series without rule, or count out of range.
/// </summary>
public sealed class SlotSearchException(string message) : Exception(message);

/// <summary>
/// Finds the next free dates of a series. Pure and deterministic: reads no files and uses no clock.
/// </summary>
public static class SlotFinder
{
    public const int MinCount = 1;
    public const int MaxCount = 100;

    /// <summary>
    /// Pieces in these statuses take their date (spec HU-05 §3).
    /// </summary>
    public static bool Occupies(Piece piece) => piece.EffectiveDate is not null && IsReady(piece.Status);

    /// <summary>
    /// 'listo', 'programado' or 'publicado'.
    /// </summary>
    public static bool IsReady(PieceStatus? status) =>
        status is PieceStatus.Ready or PieceStatus.Scheduled or PieceStatus.Published;

    /// <summary>
    /// Returns up to <paramref name="count"/> free dates of <paramref name="seriesId"/>, from <paramref name="from"/> (included).
    /// </summary>
    /// <exception cref="SlotSearchException">Unknown series, series without cadence or link, or count out of range.</exception>
    public static SlotSearch Find(SeriesConfig config, IReadOnlyList<Piece> pieces, string seriesId, DateOnly from, int count)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(pieces);

        if (count is < MinCount or > MaxCount)
        {
            throw new SlotSearchException($"La cantidad debe ser un entero entre {MinCount} y {MaxCount}.");
        }

        var id = seriesId?.Trim() ?? string.Empty;
        var series = config.Find(id) ?? throw new SlotSearchException(UnknownSeriesMessage(config, id));
        if (!HasRule(series))
        {
            throw new SlotSearchException($"La serie '{series.Id}' no tiene cadencia; no se pueden calcular huecos.");
        }

        var slots = Enumerate(series, Ordered(pieces), from, to: null).Take(count).ToList();
        return new SlotSearch(series, from, slots);
    }

    internal static bool HasRule(Series series) => series.Cadence is not null || series.After is not null;

    internal static string UnknownSeriesMessage(SeriesConfig config, string id) =>
        config.Series.Count == 0
            ? $"La serie '{id}' no existe: el fichero de series no define ninguna serie."
            : $"La serie '{id}' no existe en el fichero de series. Series disponibles: {string.Join(", ", config.Series.Select(s => s.Id))}.";

    /// <summary>
    /// Fixed order so results never depend on the order of the input.
    /// </summary>
    internal static List<Piece> Ordered(IEnumerable<Piece> pieces) =>
        pieces.OrderBy(p => p.Path, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Free dates of a series with a rule, in date order, from <paramref name="from"/> up to <paramref name="to"/> (both included).
    /// Without <paramref name="to"/>, a cadence series yields dates forever: the caller limits the count.
    /// </summary>
    /// <param name="pieces">Pieces already sorted with <see cref="Ordered"/>.</param>
    internal static IEnumerable<Slot> Enumerate(Series series, List<Piece> pieces, DateOnly from, DateOnly? to) =>
        series.After is { } link
            ? EnumerateAfter(series, link, pieces, from, to)
            : EnumerateByCadence(series, series.Cadence!, pieces, from, to);

    private static IEnumerable<Slot> EnumerateByCadence(Series series, Cadence cadence, List<Piece> pieces, DateOnly from, DateOnly? to)
    {
        var occupied = pieces
            .Where(p => p.Series == series.Id && Occupies(p))
            .Select(p => p.EffectiveDate!.Value)
            .ToHashSet();

        var last = to ?? DateOnly.MaxValue;
        for (var date = from; date <= last; date = date.AddDays(1))
        {
            if (cadence.Matches(date) && !occupied.Contains(date))
            {
                yield return new Slot(date, null, SameDay(series, pieces, date), CadenceMismatch: false);
            }

            if (date == DateOnly.MaxValue)
            {
                yield break;
            }
        }
    }

    private static IEnumerable<Slot> EnumerateAfter(Series series, SeriesLink link, List<Piece> pieces, DateOnly from, DateOnly? to)
    {
        var covered = pieces
            .Where(p => p.Series == series.Id)
            .SelectMany(p => p.DependsOn)
            .Select(PieceReference.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var last = to ?? DateOnly.MaxValue;
        return pieces
            .Where(p => p.Series == link.SeriesId && Occupies(p))
            .Where(p => !covered.Contains(PieceReference.Normalize(p.Path)))
            .Select(p => (Related: p, Day: p.EffectiveDate!.Value.DayNumber + link.Days))
            .Where(x => x.Day <= DateOnly.MaxValue.DayNumber)
            .Select(x => (x.Related, Date: DateOnly.FromDayNumber(x.Day)))
            .Where(x => x.Date >= from && x.Date <= last)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Related.Path, StringComparer.Ordinal)
            .Select(x => new Slot(
                x.Date,
                x.Related,
                SameDay(series, pieces, x.Date),
                CadenceMismatch: series.Cadence is { } cadence && !cadence.Matches(x.Date)));
    }

    private static List<Piece> SameDay(Series series, List<Piece> pieces, DateOnly date) =>
        pieces
            .Where(p => p.Series is not null && p.Series != series.Id && Occupies(p) && p.EffectiveDate == date)
            .ToList();
}

/// <summary>
/// References between pieces ('depende_de'): 'casos\01.md', 'casos/01.MD' and 'casos/01' are the same piece.
/// Compare normalized values with <see cref="StringComparer.OrdinalIgnoreCase"/>.
/// </summary>
internal static class PieceReference
{
    private const string MarkdownExtension = ".md";

    public static string Normalize(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        return normalized.EndsWith(MarkdownExtension, StringComparison.OrdinalIgnoreCase)
            ? normalized[..^MarkdownExtension.Length]
            : normalized;
    }
}
