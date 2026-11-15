using System.CommandLine;
using Ali.Cli.Output;
using Ali.Core.Content;
using Ali.Core.Scheduling;

namespace Ali.Cli.Commands;

/// <summary>
/// 'ali validar': clashes, gaps and dependency problems of the calendar.
/// </summary>
internal static class ValidateCommand
{
    private const int DefaultRangeDays = 28;

    public static Command Create(CliContext context)
    {
        var content = new Option<string?>("--contenido", "-c")
        {
            Description = $"Carpeta de contenido. Por defecto, {CliContext.ContentFolderVariable} o la carpeta actual.",
            HelpName = "carpeta",
        };
        var seriesFile = new Option<string?>("--series")
        {
            Description = SeriesFile.Description,
            HelpName = "fichero",
        };
        var from = new Option<string?>("--desde")
        {
            Description = "Inicio del rango, incluido (AAAA-MM-DD u hoy). Por defecto, hoy.",
            HelpName = "fecha",
        };
        var to = new Option<string?>("--hasta")
        {
            Description = $"Fin del rango, incluido (AAAA-MM-DD u hoy). Por defecto, {DefaultRangeDays} días después de --desde.",
            HelpName = "fecha",
        };
        var series = new Option<string[]>("--serie", "-s")
        {
            Description = "Solo los problemas de esta serie. Se puede repetir.",
            HelpName = "id",
        };
        var json = new Option<bool>("--json")
        {
            Description = "Salida en JSON con los problemas y los avisos.",
        };

        var command = new Command("validar", "Detecta choques, huecos sin cubrir, dependencias rotas y piezas bloqueadas.")
        {
            content, seriesFile, from, to, series, json,
        };

        command.SetAction(result => Execute(
            context,
            result.GetValue(content),
            result.GetValue(seriesFile),
            result.GetValue(from),
            result.GetValue(to),
            result.GetValue(series) ?? [],
            result.GetValue(json)));

        return command;
    }

    private static int Execute(
        CliContext context,
        string? contentOption,
        string? seriesFileOption,
        string? fromValue,
        string? toValue,
        string[] seriesValues,
        bool json)
    {
        if (!DateArgument.TryParse(context, fromValue, out var fromDate))
        {
            return context.Fail($"Fecha '{fromValue}' no válida en --desde. Formato esperado: AAAA-MM-DD u hoy.");
        }

        if (!DateArgument.TryParse(context, toValue, out var toDate))
        {
            return context.Fail($"Fecha '{toValue}' no válida en --hasta. Formato esperado: AAAA-MM-DD u hoy.");
        }

        var from = fromDate ?? context.Today();
        var to = toDate ?? from.AddDays(DefaultRangeDays);
        var contentFolder = context.ResolveContentFolder(contentOption);

        Catalog catalog;
        CalendarReport report;
        try
        {
            catalog = new CatalogReader().Read(contentFolder);
            var config = new SeriesConfigLoader().Load(SeriesFile.Resolve(context, seriesFileOption, contentFolder));
            report = CalendarValidator.Validate(config, catalog.Pieces, from, to, seriesValues);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or SeriesConfigException or CalendarValidationException)
        {
            return context.Fail(ex.Message);
        }

        if (json)
        {
            JsonValidationWriter.Write(context.Output, report, catalog.Warnings);
        }
        else
        {
            TextValidationWriter.Write(context.Output, report);
            TextListWriter.WriteWarnings(context.Error, catalog.Warnings);
        }

        return CliContext.Success;
    }
}
