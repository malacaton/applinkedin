using System.CommandLine;
using System.Globalization;
using Ali.Cli.Output;
using Ali.Core.Content;
using Ali.Core.Scheduling;

namespace Ali.Cli.Commands;

/// <summary>
/// 'ali huecos': next free dates of a series.
/// </summary>
internal static class SlotsCommand
{
    public const string SeriesFileVariable = "ALI_SERIES";
    public const string DefaultSeriesFile = "series.yaml";
    private const int DefaultCount = 5;

    public static Command Create(CliContext context)
    {
        var series = new Argument<string>("serie")
        {
            Description = "Identificador de la serie, tal como está en el fichero de series.",
        };
        var content = new Option<string?>("--contenido", "-c")
        {
            Description = $"Carpeta de contenido. Por defecto, {CliContext.ContentFolderVariable} o la carpeta actual.",
            HelpName = "carpeta",
        };
        var seriesFile = new Option<string?>("--series")
        {
            Description = $"Fichero de series. Por defecto, {SeriesFileVariable} o {DefaultSeriesFile} en la carpeta de contenido.",
            HelpName = "fichero",
        };
        var from = new Option<string?>("--desde")
        {
            Description = "Primera fecha candidata, incluida (AAAA-MM-DD u hoy). Por defecto, hoy.",
            HelpName = "fecha",
        };
        var count = new Option<string?>("--cantidad", "-n")
        {
            Description = $"Número de huecos, de {SlotFinder.MinCount} a {SlotFinder.MaxCount}. Por defecto, {DefaultCount}.",
            HelpName = "n",
        };
        var json = new Option<bool>("--json")
        {
            Description = "Salida en JSON con los huecos y los avisos.",
        };

        var command = new Command("huecos", "Propone las próximas fechas libres de una serie según su cadencia.")
        {
            series, content, seriesFile, from, count, json,
        };

        command.SetAction(result => Execute(
            context,
            result.GetValue(series) ?? string.Empty,
            result.GetValue(content),
            result.GetValue(seriesFile),
            result.GetValue(from),
            result.GetValue(count),
            result.GetValue(json)));

        return command;
    }

    private static int Execute(
        CliContext context,
        string seriesId,
        string? contentOption,
        string? seriesFileOption,
        string? fromValue,
        string? countValue,
        bool json)
    {
        if (!DateArgument.TryParse(context, fromValue, out var from))
        {
            return context.Fail($"Fecha '{fromValue}' no válida en --desde. Formato esperado: AAAA-MM-DD u hoy.");
        }

        var count = DefaultCount;
        if (countValue is not null
            && (!int.TryParse(countValue.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out count)
                || count is < SlotFinder.MinCount or > SlotFinder.MaxCount))
        {
            return context.Fail($"La cantidad debe ser un entero entre {SlotFinder.MinCount} y {SlotFinder.MaxCount}.");
        }

        var contentFolder = context.ResolveContentFolder(contentOption);

        Catalog catalog;
        SeriesConfig config;
        SlotSearch search;
        try
        {
            catalog = new CatalogReader().Read(contentFolder);
            config = new SeriesConfigLoader().Load(ResolveSeriesFile(context, seriesFileOption, contentFolder));
            search = SlotFinder.Find(config, catalog.Pieces, seriesId, from ?? context.Today(), count);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or SeriesConfigException or SlotSearchException)
        {
            return context.Fail(ex.Message);
        }

        if (json)
        {
            JsonSlotsWriter.Write(context.Output, search, catalog.Warnings);
        }
        else
        {
            TextSlotsWriter.Write(context.Output, search);
            TextListWriter.WriteWarnings(context.Error, catalog.Warnings);
        }

        return CliContext.Success;
    }

    private static string ResolveSeriesFile(CliContext context, string? option, string contentFolder)
    {
        if (!string.IsNullOrWhiteSpace(option))
        {
            return Path.GetFullPath(option, context.CurrentDirectory);
        }

        if (context.GetEnvironmentVariable(SeriesFileVariable) is { } fromEnvironment && !string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment, context.CurrentDirectory);
        }

        return Path.Combine(contentFolder, DefaultSeriesFile);
    }
}
