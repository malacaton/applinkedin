namespace Ali.Core.Scheduling;

/// <summary>
/// Spanish day names, with and without accents, case-insensitive.
/// </summary>
internal static class SpanishDays
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
}
