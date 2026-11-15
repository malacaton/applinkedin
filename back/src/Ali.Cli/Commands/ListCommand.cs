using System.CommandLine;
using System.Globalization;
using Ali.Cli.Output;
using Ali.Core.Content;
using Ali.Core.Input;
using Ali.Core.Output;

namespace Ali.Cli.Commands;

/// <summary>
/// 'ali listar': lists the pieces of the catalog with optional filters.
/// </summary>
internal static class ListCommand
{
    private const string DateFormat = "yyyy-MM-dd";

    public static Command Create(CliContext context)
    {
        var content = new Option<string?>("--contenido", "-c")
        {
            Description = $"Carpeta de contenido. Por defecto, {CliContext.ContentFolderVariable} o la carpeta actual.",
            HelpName = "carpeta",
        };
        var series = new Option<string[]>("--serie", "-s")
        {
            Description = "Solo piezas de esta serie. Se puede repetir.",
            HelpName = "id",
        };
        var unclassified = new Option<bool>("--sin-clasificar")
        {
            Description = "Incluye solo las piezas sin serie (o, con --serie, además de esas series).",
        };
        var statuses = new Option<string[]>("--estado", "-e")
        {
            Description = $"Solo piezas en este estado ({string.Join(", ", PieceStatusNames.All)}). Se puede repetir.",
            HelpName = "estado",
        };
        var from = new Option<string?>("--desde")
        {
            Description = "Fecha inicial, incluida (AAAA-MM-DD).",
            HelpName = "fecha",
        };
        var to = new Option<string?>("--hasta")
        {
            Description = "Fecha final, incluida (AAAA-MM-DD).",
            HelpName = "fecha",
        };
        var json = new Option<bool>("--json")
        {
            Description = "Salida en JSON con las piezas y los avisos.",
        };

        var command = new Command("listar", "Lista las piezas del catálogo, con filtros combinables.")
        {
            content, series, unclassified, statuses, from, to, json,
        };

        command.SetAction(result => Execute(
            context,
            result.GetValue(content),
            result.GetValue(series) ?? [],
            result.GetValue(unclassified),
            result.GetValue(statuses) ?? [],
            result.GetValue(from),
            result.GetValue(to),
            result.GetValue(json)));

        return command;
    }

    private static int Execute(
        CliContext context,
        string? contentOption,
        string[] seriesValues,
        bool includeUnclassified,
        string[] statusValues,
        string? fromValue,
        string? toValue,
        bool json)
    {
        var parsedStatuses = new List<PieceStatus>();
        foreach (var value in statusValues)
        {
            if (!PieceStatusNames.TryParse(value, out var status))
            {
                return context.Fail(
                    StatusInput.InvalidMessage(value));
            }

            parsedStatuses.Add(status);
        }

        if (!TryParseDate(fromValue, out var from))
        {
            return context.Fail($"Fecha '{fromValue}' no válida en --desde. Formato esperado: AAAA-MM-DD.");
        }

        if (!TryParseDate(toValue, out var to))
        {
            return context.Fail($"Fecha '{toValue}' no válida en --hasta. Formato esperado: AAAA-MM-DD.");
        }

        var query = new PieceQuery
        {
            Series = seriesValues.Select(s => s.Trim()).Where(s => s.Length > 0).ToList(),
            IncludeUnclassified = includeUnclassified,
            Statuses = parsedStatuses,
            From = from,
            To = to,
        };

        if (query.Validate() is { } invalid)
        {
            return context.Fail(invalid);
        }

        Catalog catalog;
        try
        {
            catalog = new CatalogReader().Read(context.ResolveContentFolder(contentOption));
        }
        catch (DirectoryNotFoundException ex)
        {
            return context.Fail(ex.Message);
        }

        var pieces = PieceFilter.Apply(catalog.Pieces, query);

        if (json)
        {
            context.Output.WriteLine(JsonOutput.Serialize(ListDocument.Create(pieces, catalog.Warnings)));
        }
        else
        {
            TextListWriter.Write(context.Output, pieces);
            TextListWriter.WriteWarnings(context.Error, catalog.Warnings);
        }

        return CliContext.Success;
    }

    private static bool TryParseDate(string? value, out DateOnly? date)
    {
        date = null;
        if (value is null)
        {
            return true;
        }

        if (DateOnly.TryParseExact(value.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
            return true;
        }

        return false;
    }
}
