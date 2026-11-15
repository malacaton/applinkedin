using System.CommandLine;
using Ali.Core.Content;
using Ali.Core.Input;

namespace Ali.Cli.Commands;

/// <summary>
/// 'ali estado': changes the status, dates and URL of one piece.
/// </summary>
internal static class StatusCommand
{
    private const string Missing = "—";

    public static Command Create(CliContext context)
    {
        var piece = new Argument<string>("fichero")
        {
            Description = "Pieza, con la ruta que muestra 'ali listar' (con o sin .md).",
        };
        var content = new Option<string?>("--contenido", "-c")
        {
            Description = $"Carpeta de contenido. Por defecto, {CliContext.ContentFolderVariable} o la carpeta actual.",
            HelpName = "carpeta",
        };
        var status = new Option<string?>("--estado", "-e")
        {
            Description = $"Nuevo estado ({string.Join(", ", PieceStatusNames.All)}).",
            HelpName = "estado",
        };
        var planned = new Option<string?>("--fecha-prevista")
        {
            Description = "Fecha prevista (AAAA-MM-DD u hoy).",
            HelpName = "fecha",
        };
        var published = new Option<string?>("--fecha-publicada")
        {
            Description = "Fecha de publicación (AAAA-MM-DD u hoy).",
            HelpName = "fecha",
        };
        var url = new Option<string?>("--url")
        {
            Description = "URL http o https de la pieza publicada.",
            HelpName = "url",
        };

        var command = new Command("estado", "Cambia el estado, las fechas o la URL de una pieza, tocando solo su bloque YAML.")
        {
            piece, content, status, planned, published, url,
        };

        command.SetAction(result => Execute(
            context,
            result.GetValue(piece) ?? string.Empty,
            result.GetValue(content),
            result.GetValue(status),
            result.GetValue(planned),
            result.GetValue(published),
            result.GetValue(url)));

        return command;
    }

    private static int Execute(
        CliContext context,
        string piecePath,
        string? contentOption,
        string? statusValue,
        string? plannedValue,
        string? publishedValue,
        string? urlValue)
    {
        PieceStatus? status = null;
        if (statusValue is not null)
        {
            if (!PieceStatusNames.TryParse(statusValue, out var parsed))
            {
                return context.Fail(
                    StatusInput.InvalidMessage(statusValue));
            }

            status = parsed;
        }

        if (!DateInput.TryParse(plannedValue, context.Today(), out var plannedDate))
        {
            return context.Fail(DateInput.InvalidMessage(plannedValue, "--fecha-prevista"));
        }

        if (!DateInput.TryParse(publishedValue, context.Today(), out var publishedDate))
        {
            return context.Fail(DateInput.InvalidMessage(publishedValue, "--fecha-publicada"));
        }

        var changes = new PieceChanges
        {
            Status = status,
            PlannedDate = plannedDate,
            PublishedDate = publishedDate,
            Url = urlValue?.Trim(),
        };

        PieceUpdateResult result;
        try
        {
            result = new PieceUpdater().Update(context.ResolveContentFolder(contentOption), piecePath, changes);
        }
        catch (Exception ex) when (ex is PieceUpdateException or DirectoryNotFoundException)
        {
            return context.Fail(ex.Message);
        }

        if (!result.HasChanges)
        {
            context.Output.WriteLine($"Sin cambios en {result.Path}.");
            return CliContext.Success;
        }

        context.Output.WriteLine($"Actualizado {result.Path}:");
        foreach (var change in result.Changes)
        {
            context.Output.WriteLine($"  {change.Field}: {change.OldValue ?? Missing} → {change.NewValue}");
        }

        return CliContext.Success;
    }
}
