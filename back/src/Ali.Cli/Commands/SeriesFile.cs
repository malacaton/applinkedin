namespace Ali.Cli.Commands;

/// <summary>
/// Location of the series configuration file.
/// </summary>
internal static class SeriesFile
{
    public const string Variable = "ALI_SERIES";
    public const string DefaultName = "series.yaml";

    public static string Description =>
        $"Fichero de series. Por defecto, {Variable} o {DefaultName} en la carpeta de contenido.";

    /// <summary>
    /// Explicit option, then ALI_SERIES, then series.yaml in the content folder.
    /// </summary>
    public static string Resolve(CliContext context, string? option, string contentFolder)
    {
        if (!string.IsNullOrWhiteSpace(option))
        {
            return Path.GetFullPath(option, context.CurrentDirectory);
        }

        if (context.GetEnvironmentVariable(Variable) is { } fromEnvironment && !string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment, context.CurrentDirectory);
        }

        return Path.Combine(contentFolder, DefaultName);
    }
}
