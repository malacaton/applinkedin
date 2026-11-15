using System.Globalization;
using Ali.Core.Content;

namespace Ali.Core.Input;

/// <summary>
/// Dates typed by the user: 'AAAA-MM-DD' or the word "hoy".
/// </summary>
public static class DateInput
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string TodayWord = "hoy";

    /// <summary>
    /// A null value is valid and gives a null date. "hoy" is resolved with <paramref name="today"/>.
    /// </summary>
    public static bool TryParse(string? value, DateOnly today, out DateOnly? date)
    {
        date = null;
        if (value is null)
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Equals(TodayWord, StringComparison.OrdinalIgnoreCase))
        {
            date = today;
            return true;
        }

        if (DateOnly.TryParseExact(trimmed, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
            return true;
        }

        return false;
    }

    /// <param name="parameter">Name shown to the user: '--desde' in the CLI, 'desde' in the MCP server.</param>
    public static string InvalidMessage(string? value, string parameter) =>
        $"Fecha '{value}' no válida en {parameter}. Formato esperado: AAAA-MM-DD u hoy.";
}

/// <summary>
/// Statuses typed by the user.
/// </summary>
public static class StatusInput
{
    public static string InvalidMessage(string? value) =>
        $"Estado '{value}' no válido. Valores admitidos: {string.Join(", ", PieceStatusNames.All)}.";
}

/// <summary>
/// Where the content folder and the series file are.
/// </summary>
public static class ContentLocation
{
    public const string ContentFolderVariable = "ALI_CONTENIDO";
    public const string SeriesFileVariable = "ALI_SERIES";
    public const string DefaultSeriesFileName = "series.yaml";

    /// <summary>
    /// Explicit value, then ALI_SERIES, then series.yaml in the content folder. Relative paths use <paramref name="baseDirectory"/>.
    /// </summary>
    public static string ResolveSeriesFile(
        string? option,
        Func<string, string?> getEnvironmentVariable,
        string contentFolder,
        string baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(option))
        {
            return Path.GetFullPath(option, baseDirectory);
        }

        if (getEnvironmentVariable(SeriesFileVariable) is { } fromEnvironment && !string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment, baseDirectory);
        }

        return Path.Combine(contentFolder, DefaultSeriesFileName);
    }
}
