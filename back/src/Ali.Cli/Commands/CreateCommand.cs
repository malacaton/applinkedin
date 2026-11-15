using System.CommandLine;
using System.Globalization;
using Ali.Core.Content;
using Ali.Core.Scheduling;

namespace Ali.Cli.Commands;

/// <summary>
/// 'ali crear': creates the file of a new piece with the series naming convention.
/// </summary>
internal static class CreateCommand
{
    private const string DateFormat = "yyyy-MM-dd";

    public static Command Create(CliContext context)
    {
        var series = new Argument<string>("serie")
        {
            Description = "Identificador de la serie, tal como está en el fichero de series.",
        };
        var title = new Argument<string>("titulo")
        {
            Description = "Título de la pieza (entre comillas si lleva espacios).",
        };
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
        var date = new Option<string?>("--fecha")
        {
            Description = "Fecha prevista (AAAA-MM-DD u hoy).",
            HelpName = "fecha",
        };
        var firstFree = new Option<bool>("--fecha-libre")
        {
            Description = "Asigna como fecha prevista la primera fecha libre de la serie.",
        };
        var from = new Option<string?>("--desde")
        {
            Description = "Con --fecha-libre, desde qué fecha buscar (AAAA-MM-DD u hoy). Por defecto, hoy.",
            HelpName = "fecha",
        };
        var format = new Option<string?>("--formato")
        {
            Description = "Formato de la pieza (articulo, post, carrusel...).",
            HelpName = "formato",
        };
        var dependsOn = new Option<string[]>("--depende-de")
        {
            Description = "Pieza de la que depende. Se puede repetir.",
            HelpName = "pieza",
        };

        var command = new Command("crear", "Crea el fichero de una pieza nueva con el nombre y el bloque YAML de su serie.")
        {
            series, title, content, seriesFile, date, firstFree, from, format, dependsOn,
        };

        command.SetAction(result => Execute(
            context,
            result.GetValue(series) ?? string.Empty,
            result.GetValue(title) ?? string.Empty,
            result.GetValue(content),
            result.GetValue(seriesFile),
            result.GetValue(date),
            result.GetValue(firstFree),
            result.GetValue(from),
            result.GetValue(format),
            result.GetValue(dependsOn) ?? []));

        return command;
    }

    private static int Execute(
        CliContext context,
        string seriesId,
        string title,
        string? contentOption,
        string? seriesFileOption,
        string? dateValue,
        bool firstFree,
        string? fromValue,
        string? format,
        string[] dependsOn)
    {
        if (!DateArgument.TryParse(context, dateValue, out var plannedDate))
        {
            return context.Fail($"Fecha '{dateValue}' no válida en --fecha. Formato esperado: AAAA-MM-DD u hoy.");
        }

        if (!DateArgument.TryParse(context, fromValue, out var from))
        {
            return context.Fail($"Fecha '{fromValue}' no válida en --desde. Formato esperado: AAAA-MM-DD u hoy.");
        }

        var contentFolder = context.ResolveContentFolder(contentOption);

        CreatedPiece created;
        try
        {
            var catalog = new CatalogReader().Read(contentFolder);
            var config = new SeriesConfigLoader().Load(SeriesFile.Resolve(context, seriesFileOption, contentFolder));
            created = new PieceCreator().Create(contentFolder, config, catalog.Pieces, new NewPiece
            {
                SeriesId = seriesId,
                Title = title,
                PlannedDate = plannedDate,
                UseFirstFreeDate = firstFree,
                SearchFrom = from ?? context.Today(),
                Format = format,
                DependsOn = dependsOn,
            });
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or SeriesConfigException or PieceCreationException)
        {
            return context.Fail(ex.Message);
        }

        var piece = created.Piece;
        context.Output.WriteLine($"Creada {created.Path}");
        context.Output.WriteLine($"  serie: {piece.Series}");
        context.Output.WriteLine($"  estado: {PieceStatusNames.ToName(piece.Status!.Value)}");
        if (piece.PlannedDate is { } planned)
        {
            context.Output.WriteLine($"  fecha_prevista: {planned.ToString(DateFormat, CultureInfo.InvariantCulture)}");
        }

        if (piece.Format is not null)
        {
            context.Output.WriteLine($"  formato: {piece.Format}");
        }

        if (piece.DependsOn.Count > 0)
        {
            context.Output.WriteLine($"  depende_de: {string.Join(", ", piece.DependsOn)}");
        }

        return CliContext.Success;
    }
}
