namespace Ali.Core.Content;

/// <summary>
/// Filters for listing pieces. Different filters combine with AND; values of the same filter with OR.
/// An empty query means "no filters".
/// </summary>
public sealed record PieceQuery
{
    /// <summary>
    /// Series ids to include. Empty means any series.
    /// </summary>
    public IReadOnlyList<string> Series { get; init; } = [];

    /// <summary>
    /// Include unclassified pieces (no series). Combined with <see cref="Series"/> with OR.
    /// </summary>
    public bool IncludeUnclassified { get; init; }

    /// <summary>
    /// Statuses to include. Empty means any status (including none).
    /// </summary>
    public IReadOnlyList<PieceStatus> Statuses { get; init; } = [];

    /// <summary>
    /// First date of the range, inclusive. Pieces without date are excluded when set.
    /// </summary>
    public DateOnly? From { get; init; }

    /// <summary>
    /// Last date of the range, inclusive. Pieces without date are excluded when set.
    /// </summary>
    public DateOnly? To { get; init; }

    /// <summary>
    /// Returns the reason why the query is invalid, in Spanish, or null if it is valid.
    /// </summary>
    public string? Validate() =>
        From is { } from && To is { } to && from > to
            ? $"La fecha 'desde' ({from:yyyy-MM-dd}) es posterior a 'hasta' ({to:yyyy-MM-dd})."
            : null;
}

/// <summary>
/// Applies a <see cref="PieceQuery"/> and sorts the result.
/// </summary>
public static class PieceFilter
{
    /// <summary>
    /// Filters <paramref name="pieces"/> and sorts them chronologically by <see cref="Piece.EffectiveDate"/>.
    /// Pieces without date go last; ties are broken by path.
    /// </summary>
    /// <exception cref="ArgumentException">The query is invalid (see <see cref="PieceQuery.Validate"/>).</exception>
    public static IReadOnlyList<Piece> Apply(IEnumerable<Piece> pieces, PieceQuery query)
    {
        if (query.Validate() is { } error)
        {
            throw new ArgumentException(error, nameof(query));
        }

        return pieces
            .Where(p => MatchesSeries(p, query))
            .Where(p => query.Statuses.Count == 0 || (p.Status is { } status && query.Statuses.Contains(status)))
            .Where(p => MatchesRange(p, query))
            .OrderBy(p => p.EffectiveDate is null)
            .ThenBy(p => p.EffectiveDate)
            .ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Path, StringComparer.Ordinal)
            .ToList();
    }

    private static bool MatchesSeries(Piece piece, PieceQuery query)
    {
        if (query.Series.Count == 0 && !query.IncludeUnclassified)
        {
            return true;
        }

        return piece.Series is null
            ? query.IncludeUnclassified
            : query.Series.Contains(piece.Series, StringComparer.Ordinal);
    }

    private static bool MatchesRange(Piece piece, PieceQuery query)
    {
        if (query.From is null && query.To is null)
        {
            return true;
        }

        return piece.EffectiveDate is { } date
            && (query.From is null || date >= query.From)
            && (query.To is null || date <= query.To);
    }
}
