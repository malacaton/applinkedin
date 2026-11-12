namespace Ali.Core.Scheduling;

/// <summary>
/// A content series. Pieces reference it through their 'serie' field (<see cref="Id"/>).
/// </summary>
/// <param name="Id">Unique identifier (lowercase letters, digits and hyphens).</param>
/// <param name="Name">Display name.</param>
/// <param name="Cadence">Publication rule; null means free publication.</param>
/// <param name="After">Link to another series whose pieces this one follows; null if none.</param>
public sealed record Series(string Id, string Name, Cadence? Cadence, SeriesLink? After);

/// <summary>
/// The pieces of a series go <paramref name="Days"/> days after their related piece in <paramref name="SeriesId"/>.
/// </summary>
public sealed record SeriesLink(string SeriesId, int Days);

public enum CadenceFrequency
{
    Weekly,
    Biweekly,
}

/// <summary>
/// Publication rule of a series.
/// </summary>
public sealed record Cadence
{
    public Cadence(IReadOnlyList<DayOfWeek> days, CadenceFrequency frequency, DateOnly? anchor, TimeOnly? time)
    {
        if (days.Count == 0)
        {
            throw new ArgumentException("A cadence needs at least one day.", nameof(days));
        }

        if (frequency == CadenceFrequency.Biweekly && anchor is null)
        {
            throw new ArgumentException("A biweekly cadence needs an anchor date.", nameof(anchor));
        }

        Days = days;
        Frequency = frequency;
        Anchor = anchor;
        Time = time;
    }

    /// <summary>
    /// Publication days, in the order they were declared.
    /// </summary>
    public IReadOnlyList<DayOfWeek> Days { get; }

    public CadenceFrequency Frequency { get; }

    /// <summary>
    /// Any date in a publication week (required for <see cref="CadenceFrequency.Biweekly"/>).
    /// </summary>
    public DateOnly? Anchor { get; }

    /// <summary>
    /// Approximate publication time, local.
    /// </summary>
    public TimeOnly? Time { get; }

    /// <summary>
    /// True when <paramref name="date"/> is a publication day for this cadence.
    /// </summary>
    public bool Matches(DateOnly date)
    {
        if (!Days.Contains(date.DayOfWeek))
        {
            return false;
        }

        if (Frequency == CadenceFrequency.Weekly)
        {
            return true;
        }

        var weeks = (MondayOf(date).DayNumber - MondayOf(Anchor!.Value).DayNumber) / 7;
        return weeks % 2 == 0;
    }

    private static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public bool Equals(Cadence? other) =>
        other is not null
        && Days.SequenceEqual(other.Days)
        && Frequency == other.Frequency
        && Anchor == other.Anchor
        && Time == other.Time;

    public override int GetHashCode() => HashCode.Combine(Days.Count, Frequency, Anchor, Time);
}

/// <summary>
/// Loaded series configuration.
/// </summary>
public sealed class SeriesConfig
{
    private readonly Dictionary<string, Series> byId;

    public SeriesConfig(IReadOnlyList<Series> series)
    {
        Series = series;
        byId = series.ToDictionary(s => s.Id, StringComparer.Ordinal);
    }

    /// <summary>
    /// Series in file order.
    /// </summary>
    public IReadOnlyList<Series> Series { get; }

    public Series? Find(string id) => byId.GetValueOrDefault(id);
}
