namespace Ali.Core.Scheduling;

/// <summary>
/// Spanish day names, with and without accents, case-insensitive.
/// </summary>
public static class SpanishDays
{
    private static readonly Dictionary<string, DayOfWeek> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lunes"] = DayOfWeek.Monday,
        ["martes"] = DayOfWeek.Tuesday,
        ["miércoles"] = DayOfWeek.Wednesday,
        ["miercoles"] = DayOfWeek.Wednesday,
        ["jueves"] = DayOfWeek.Thursday,
        ["viernes"] = DayOfWeek.Friday,
        ["sábado"] = DayOfWeek.Saturday,
        ["sabado"] = DayOfWeek.Saturday,
        ["domingo"] = DayOfWeek.Sunday,
    };

    public static bool TryParse(string value, out DayOfWeek day) => ByName.TryGetValue(value.Trim(), out day);

    /// <summary>
    /// Name with accents, lowercase: "lunes" … "domingo".
    /// </summary>
    public static string ToName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "lunes",
        DayOfWeek.Tuesday => "martes",
        DayOfWeek.Wednesday => "miércoles",
        DayOfWeek.Thursday => "jueves",
        DayOfWeek.Friday => "viernes",
        DayOfWeek.Saturday => "sábado",
        DayOfWeek.Sunday => "domingo",
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, null),
    };
}
