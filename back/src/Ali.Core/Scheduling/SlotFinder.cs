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

    private const string MarkdownExtension = ".md";

    /// <summary>
    /// Pieces in these statuses take their date (spec §3).
    /// </summary>
    public static bool Occupies(Piece piece) =>
        piece.EffectiveDate is not null
        && piece.Status is PieceStatus.Ready or PieceStatus.Scheduled or PieceStatus.Published;

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
        var series = config.Find(id) ?? throw UnknownSeries(config, id);
        if (series.Cadence is null && series.After is null)
        {
            throw new SlotSearchException($"La serie '{series.Id}' no tiene cadencia; no se pueden calcular huecos.");
        }

        // Fixed order so the result never depends on the order of the input.
        var ordered = pieces.OrderBy(p => p.Path, StringComparer.Ordinal).ToList();

        var slots = series.After is { } link
            ? FindAfter(series, link, ordered, from, count)
            : FindByCadence(series, series.Cadence!, ordered, from, count);

        return new SlotSearch(series, from, slots);
    }

    private static SlotSearchException UnknownSeries(SeriesConfig config, string id) =>
        config.Series.Count == 0
            ? new SlotSearchException($"La serie '{id}' no existe: el fichero de series no define ninguna serie.")
            : new SlotSearchException(
                $"La serie '{id}' no existe en el fichero de series. Series disponibles: {string.Join(", ", config.Series.Select(s => s.Id))}.");

    private static List<Slot> FindByCadence(Series series, Cadence cadence, List<Piece> pieces, DateOnly from, int count)
    {
        var occupied = pieces
            .Where(p => p.Series == series.Id && Occupies(p))
            .Select(p => p.EffectiveDate!.Value)
            .ToHashSet();

        var slots = new List<Slot>(count);
        var date = from;
        while (slots.Count < count)
        {
            if (cadence.Matches(date) && !occupied.Contains(date))
            {
                slots.Add(new Slot(date, null, SameDay(series, pieces, date), CadenceMismatch: false));
            }

            if (date == DateOnly.MaxValue)
            {
                break;
            }

            date = date.AddDays(1);
        }

        return slots;
    }

    private static List<Slot> FindAfter(Series series, SeriesLink link, List<Piece> pieces, DateOnly from, int count)
    {
        var covered = pieces
            .Where(p => p.Series == series.Id)
            .SelectMany(p => p.DependsOn)
            .Select(NormalizeReference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return pieces
            .Where(p => p.Series == link.SeriesId && Occupies(p))
            .Where(p => !covered.Contains(NormalizeReference(p.Path)))
            .Select(p => (Related: p, Date: p.EffectiveDate!.Value.DayNumber + link.Days))
            .Where(x => x.Date <= DateOnly.MaxValue.DayNumber)
            .Select(x => (x.Related, Date: DateOnly.FromDayNumber(x.Date)))
            .Where(x => x.Date >= from)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Related.Path, StringComparer.Ordinal)
            .Take(count)
            .Select(x => new Slot(
                x.Date,
                x.Related,
                SameDay(series, pieces, x.Date),
                CadenceMismatch: series.Cadence is { } cadence && !cadence.Matches(x.Date)))
            .ToList();
    }

    private static List<Piece> SameDay(Series series, List<Piece> pieces, DateOnly date) =>
        pieces
            .Where(p => p.Series is not null && p.Series != series.Id && Occupies(p) && p.EffectiveDate == date)
            .ToList();

    /// <summary>
    /// 'casos\01.md', 'casos/01.MD' and 'casos/01' all refer to the same piece.
    /// </summary>
    private static string NormalizeReference(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        return normalized.EndsWith(MarkdownExtension, StringComparison.OrdinalIgnoreCase)
            ? normalized[..^MarkdownExtension.Length]
            : normalized;
    }
}
