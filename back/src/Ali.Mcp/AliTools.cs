using System.ComponentModel;
using Ali.Core.Content;
using Ali.Core.Input;
using Ali.Core.Output;
using Ali.Core.Scheduling;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Ali.Mcp;

/// <summary>
/// The MCP tools. A thin layer over the core: the same calls and the same JSON as the CLI.
/// Catalog and series are read again on every call, so manual edits are always visible.
/// Parameter names are part of the protocol contract and use the Spanish names of the CLI and the YAML.
/// </summary>
[McpServerToolType]
public sealed class AliTools(ServerSettings settings, TimeProvider clock)
{
    private const string DateHelp = "Fecha AAAA-MM-DD u 'hoy'.";
    private const int DefaultSlotCount = 5;
    private const int DefaultRangeDays = 28;

    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    [McpServerTool(Name = "listar_piezas", Title = "Listar piezas", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lista las piezas de contenido (ficheros .md) con sus metadatos: serie, estado, fechas, URL, dependencias y formato. "
        + "Los filtros se combinan con Y entre ellos y con O dentro de cada lista. Devuelve JSON con 'piezas' y 'avisos' "
        + "(ficheros con metadatos inválidos). Ordenadas por fecha; sin fecha al final.")]
    public string ListPieces(
        [Description("Solo piezas de estas series (id de serie).")] string[]? serie = null,
        [Description("Incluye las piezas sin serie. Sin 'serie', muestra solo esas.")] bool sin_clasificar = false,
        [Description("Solo piezas en estos estados: idea, borrador, listo, programado o publicado.")] string[]? estado = null,
        [Description("Desde esta fecha, incluida (fecha publicada o, si no hay, prevista). " + DateHelp)] string? desde = null,
        [Description("Hasta esta fecha, incluida. " + DateHelp)] string? hasta = null) => Run(() =>
    {
        var statuses = new List<PieceStatus>();
        foreach (var value in estado ?? [])
        {
            statuses.Add(PieceStatusNames.TryParse(value, out var status)
                ? status
                : throw new McpException(StatusInput.InvalidMessage(value)));
        }

        var query = new PieceQuery
        {
            Series = (serie ?? []).Select(s => s.Trim()).Where(s => s.Length > 0).ToList(),
            IncludeUnclassified = sin_clasificar,
            Statuses = statuses,
            From = ParseDate(desde, nameof(desde)),
            To = ParseDate(hasta, nameof(hasta)),
        };

        if (query.Validate() is { } invalid)
        {
            throw new McpException(invalid);
        }

        var catalog = ReadCatalog();
        return JsonOutput.Serialize(ListDocument.Create(PieceFilter.Apply(catalog.Pieces, query), catalog.Warnings));
    });

    [McpServerTool(Name = "buscar_huecos", Title = "Buscar huecos libres", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Propone las próximas fechas libres de una serie según su cadencia (o su regla 'despues_de'). "
        + "Ocupan fecha las piezas en listo, programado o publicado. 'coincide_con' muestra, solo como información, "
        + "piezas de otras series el mismo día. Devuelve JSON con 'serie', 'desde', 'huecos' y 'avisos'.")]
    public string FindSlots(
        [Description("Id de la serie, tal como está en el fichero de series.")] string serie,
        [Description("Primera fecha candidata, incluida. Por defecto, hoy. " + DateHelp)] string? desde = null,
        [Description("Número de huecos, de 1 a 100. Por defecto, 5.")] int cantidad = DefaultSlotCount) => Run(() =>
    {
        var from = ParseDate(desde, nameof(desde)) ?? Today;
        var catalog = ReadCatalog();
        var search = SlotFinder.Find(LoadSeries(), catalog.Pieces, serie, from, cantidad);
        return JsonOutput.Serialize(SlotsDocument.Create(search, catalog.Warnings));
    });

    [McpServerTool(Name = "validar_calendario", Title = "Validar el calendario", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Revisa el calendario y devuelve los problemas: choque, hueco, fuera_de_cadencia, desfase, orden, bloqueada, "
        + "dependencia_rota y serie_desconocida. Los de fecha se buscan en el rango; los de dependencias y series, en todo el catálogo. "
        + "Devuelve JSON con 'desde', 'hasta', 'problemas' (tipo, fecha, serie, piezas, detalle) y 'avisos'.")]
    public string ValidateCalendar(
        [Description("Inicio del rango, incluido. Por defecto, hoy. " + DateHelp)] string? desde = null,
        [Description("Fin del rango, incluido. Por defecto, 28 días después de 'desde'; como mucho, un año. " + DateHelp)] string? hasta = null,
        [Description("Solo los problemas de estas series.")] string[]? serie = null) => Run(() =>
    {
        var from = ParseDate(desde, nameof(desde)) ?? Today;
        var to = ParseDate(hasta, nameof(hasta)) ?? from.AddDays(DefaultRangeDays);
        var catalog = ReadCatalog();
        var report = CalendarValidator.Validate(LoadSeries(), catalog.Pieces, from, to, serie ?? []);
        return JsonOutput.Serialize(ReportDocument.Create(report, catalog.Warnings));
    });

    [McpServerTool(Name = "cambiar_estado", Title = "Cambiar el estado de una pieza", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Cambia el estado, las fechas o la URL de una pieza editando solo su bloque YAML; el resto del fichero queda intacto. "
        + "Marcar como 'publicado' exige fecha_publicada (indicada ahora o ya existente). Si algo no es válido, el fichero no se toca. "
        + "Devuelve JSON con 'fichero' y 'cambios' (campo, antes, despues); 'cambios' vacío si ya tenía esos valores.")]
    public string ChangeStatus(
        [Description("Pieza, con la ruta relativa que devuelve listar_piezas (con o sin .md).")] string fichero,
        [Description("Nuevo estado: idea, borrador, listo, programado o publicado.")] string? estado = null,
        [Description("Nueva fecha prevista. " + DateHelp)] string? fecha_prevista = null,
        [Description("Nueva fecha de publicación. " + DateHelp)] string? fecha_publicada = null,
        [Description("URL http o https de la pieza publicada.")] string? url = null) => Run(() =>
    {
        PieceStatus? status = null;
        if (estado is not null)
        {
            status = PieceStatusNames.TryParse(estado, out var parsed)
                ? parsed
                : throw new McpException(StatusInput.InvalidMessage(estado));
        }

        var changes = new PieceChanges
        {
            Status = status,
            PlannedDate = ParseDate(fecha_prevista, nameof(fecha_prevista)),
            PublishedDate = ParseDate(fecha_publicada, nameof(fecha_publicada)),
            Url = url?.Trim(),
        };

        var result = new PieceUpdater().Update(settings.ContentFolder, fichero, changes);
        return JsonOutput.Serialize(UpdateDocument.Create(result));
    });

    [McpServerTool(Name = "crear_pieza", Title = "Crear una pieza nueva", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Crea el fichero .md de una pieza nueva en la carpeta de su serie, con el nombre según su convención y un bloque YAML "
        + "con serie y estado idea. Nunca sobrescribe: si el fichero existe, devuelve un error. Con fecha_libre asigna la primera "
        + "fecha libre de la serie (y, en series con 'despues_de', la dependencia). Devuelve JSON con el fichero creado y sus metadatos.")]
    public string CreatePiece(
        [Description("Id de la serie.")] string serie,
        [Description("Título de la pieza; también da nombre al fichero.")] string titulo,
        [Description("Fecha prevista. " + DateHelp + " No se combina con fecha_libre.")] string? fecha = null,
        [Description("Asigna como fecha prevista la primera fecha libre de la serie.")] bool fecha_libre = false,
        [Description("Con fecha_libre, desde qué fecha buscar. Por defecto, hoy. " + DateHelp)] string? desde = null,
        [Description("Formato: articulo, post, carrusel...")] string? formato = null,
        [Description("Piezas de las que depende (rutas relativas, con o sin .md).")] string[]? depende_de = null) => Run(() =>
    {
        var request = new NewPiece
        {
            SeriesId = serie,
            Title = titulo,
            PlannedDate = ParseDate(fecha, nameof(fecha)),
            UseFirstFreeDate = fecha_libre,
            SearchFrom = ParseDate(desde, nameof(desde)) ?? Today,
            Format = formato,
            DependsOn = depende_de ?? [],
        };

        var created = new PieceCreator().Create(settings.ContentFolder, LoadSeries(), ReadCatalog().Pieces, request);
        return JsonOutput.Serialize(CreatedDocument.Create(created));
    });

    private Catalog ReadCatalog() => new CatalogReader().Read(settings.ContentFolder);

    private SeriesConfig LoadSeries() => new SeriesConfigLoader().Load(settings.SeriesFile);

    private DateOnly? ParseDate(string? value, string parameter) =>
        DateInput.TryParse(value, Today, out var date) ? date : throw new McpException(DateInput.InvalidMessage(value, parameter));

    /// <summary>
    /// Expected problems of the data or the request become tool errors with their Spanish message.
    /// </summary>
    private static string Run(Func<string> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException
            or SeriesConfigException
            or SlotSearchException
            or CalendarValidationException
            or PieceUpdateException
            or PieceCreationException)
        {
            throw new McpException(ex.Message, ex);
        }
    }
}
