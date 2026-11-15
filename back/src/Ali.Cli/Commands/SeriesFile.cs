using Ali.Core.Input;

namespace Ali.Cli.Commands;

/// <summary>
/// Location of the series configuration file.
/// </summary>
internal static class SeriesFile
{
    public static string Description =>
        $"Fichero de series. Por defecto, {ContentLocation.SeriesFileVariable} o {ContentLocation.DefaultSeriesFileName} en la carpeta de contenido.";

    /// <summary>
    /// Explicit option, then ALI_SERIES, then series.yaml in the content folder.
    /// </summary>
    public static string Resolve(CliContext context, string? option, string contentFolder) =>
        ContentLocation.ResolveSeriesFile(option, context.GetEnvironmentVariable, contentFolder, context.CurrentDirectory);
}
