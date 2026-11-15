using System.Globalization;

namespace Ali.Cli.Commands;

/// <summary>
/// Date options: 'AAAA-MM-DD', or the word "hoy" resolved with the context clock.
/// </summary>
internal static class DateArgument
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string TodayWord = "hoy";

    /// <summary>
    /// A null value is valid and gives a null date.
    /// </summary>
    public static bool TryParse(CliContext context, string? value, out DateOnly? date)
    {
        date = null;
        if (value is null)
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Equals(TodayWord, StringComparison.OrdinalIgnoreCase))
        {
            date = context.Today();
            return true;
        }

        if (DateOnly.TryParseExact(trimmed, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
            return true;
        }

        return false;
    }
}
