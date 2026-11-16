using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ali.Core.Content;
using Ali.Core.Scheduling;

namespace Ali.Core.Output;

/// <summary>
/// JSON contract shared by the CLI (--json) and the MCP server. Field names match the YAML front matter, in Spanish.
/// </summary>
public static class JsonOutput
{
    public const string DateFormat = "yyyy-MM-dd";

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,

        // Output goes to a terminal, a tool or an AI client, never into HTML: keep accents readable.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize<T>(T document) => JsonSerializer.Serialize(document, Options);

    internal static string? Format(DateOnly? date) => date?.ToString(DateFormat, CultureInfo.InvariantCulture);

    internal static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
}

/// <summary>
/// A catalog warning ('avisos').
/// </summary>
public sealed record WarningDto(
    [property: JsonPropertyName("fichero")] string Path,
    [property: JsonPropertyName("codigo")] string Code,
    [property: JsonPropertyName("campo")] string? Field,
    [property: JsonPropertyName("motivo")] string Message)
{
    public static WarningDto Create(CatalogWarning warning) =>
        new(warning.Path, warning.Code.ToString(), warning.Field, warning.Message);
}

/// <summary>
/// A piece of the catalog.
/// </summary>
public sealed record PieceDto(
    [property: JsonPropertyName("fichero")] string Path,
    [property: JsonPropertyName("serie")] string? Series,
    [property: JsonPropertyName("estado")] string? Status,
    [property: JsonPropertyName("fecha")] string? Date,
    [property: JsonPropertyName("hora")] string? Time,
    [property: JsonPropertyName("fecha_prevista")] string? PlannedDate,
    [property: JsonPropertyName("fecha_publicada")] string? PublishedDate,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("depende_de")] IReadOnlyList<string> DependsOn,
    [property: JsonPropertyName("formato")] string? Format)
{
    public static PieceDto Create(Piece piece) => new(
        piece.Path,
        piece.Series,
        piece.Status is { } status ? PieceStatusNames.ToName(status) : null,
        JsonOutput.Format(piece.EffectiveDate),
        piece.Time?.ToString("HH:mm", CultureInfo.InvariantCulture),
        JsonOutput.Format(piece.PlannedDate),
        JsonOutput.Format(piece.PublishedDate),
        piece.Url,
        piece.DependsOn,
        piece.Format);
}

/// <summary>
/// 'listar': pieces and warnings.
/// </summary>
public sealed record ListDocument(
    [property: JsonPropertyName("piezas")] IReadOnlyList<PieceDto> Pieces,
    [property: JsonPropertyName("avisos")] IReadOnlyList<WarningDto> Warnings)
{
    public static ListDocument Create(IReadOnlyList<Piece> pieces, IReadOnlyList<CatalogWarning> warnings) =>
        new(pieces.Select(PieceDto.Create).ToList(), warnings.Select(WarningDto.Create).ToList());
}

/// <summary>
/// One free date of 'huecos'.
/// </summary>
public sealed record SlotDto(
    [property: JsonPropertyName("fecha")] string Date,
    [property: JsonPropertyName("dia")] string Day,
    [property: JsonPropertyName("tras")] string? After,
    [property: JsonPropertyName("coincide_con")] IReadOnlyList<string> SameDay,
    [property: JsonPropertyName("aviso")] string? Warning)
{
    public const string CadenceMismatchWarning = "La fecha no coincide con la cadencia de la serie.";

    public static SlotDto Create(Slot slot) => new(
        JsonOutput.Format(slot.Date),
        SpanishDays.ToName(slot.Date.DayOfWeek),
        slot.After?.Path,
        slot.SameDay.Select(p => p.Path).ToList(),
        slot.CadenceMismatch ? CadenceMismatchWarning : null);
}

/// <summary>
/// 'huecos': series, start date, free dates and warnings.
/// </summary>
public sealed record SlotsDocument(
    [property: JsonPropertyName("serie")] string Series,
    [property: JsonPropertyName("desde")] string From,
    [property: JsonPropertyName("huecos")] IReadOnlyList<SlotDto> Slots,
    [property: JsonPropertyName("avisos")] IReadOnlyList<WarningDto> Warnings)
{
    public static SlotsDocument Create(SlotSearch search, IReadOnlyList<CatalogWarning> warnings) => new(
        search.Series.Id,
        JsonOutput.Format(search.From),
        search.Slots.Select(SlotDto.Create).ToList(),
        warnings.Select(WarningDto.Create).ToList());
}

/// <summary>
/// One problem of 'validar'.
/// </summary>
public sealed record ProblemDto(
    [property: JsonPropertyName("tipo")] string Kind,
    [property: JsonPropertyName("fecha")] string? Date,
    [property: JsonPropertyName("serie")] string? Series,
    [property: JsonPropertyName("piezas")] IReadOnlyList<string> Pieces,
    [property: JsonPropertyName("detalle")] string Message)
{
    public static ProblemDto Create(CalendarProblem problem) => new(
        ProblemKindNames.ToName(problem.Kind),
        JsonOutput.Format(problem.Date),
        problem.Series,
        problem.Pieces,
        problem.Message);
}

/// <summary>
/// 'validar': range, problems and warnings.
/// </summary>
public sealed record ReportDocument(
    [property: JsonPropertyName("desde")] string From,
    [property: JsonPropertyName("hasta")] string To,
    [property: JsonPropertyName("problemas")] IReadOnlyList<ProblemDto> Problems,
    [property: JsonPropertyName("avisos")] IReadOnlyList<WarningDto> Warnings)
{
    public static ReportDocument Create(CalendarReport report, IReadOnlyList<CatalogWarning> warnings) => new(
        JsonOutput.Format(report.From),
        JsonOutput.Format(report.To),
        report.Problems.Select(ProblemDto.Create).ToList(),
        warnings.Select(WarningDto.Create).ToList());
}

/// <summary>
/// One changed field of 'cambiar_estado'.
/// </summary>
public sealed record FieldChangeDto(
    [property: JsonPropertyName("campo")] string Field,
    [property: JsonPropertyName("antes")] string? OldValue,
    [property: JsonPropertyName("despues")] string NewValue);

/// <summary>
/// 'cambiar_estado': file and changes (empty when nothing changed).
/// </summary>
public sealed record UpdateDocument(
    [property: JsonPropertyName("fichero")] string Path,
    [property: JsonPropertyName("cambios")] IReadOnlyList<FieldChangeDto> Changes,
    [property: JsonPropertyName("renombrado")] IReadOnlyList<RenamedDto> Renamed,
    [property: JsonPropertyName("aviso")] string? Warning)
{
    public static UpdateDocument Create(PieceUpdateResult result) => new(
        result.Path,
        result.Changes.Select(c => new FieldChangeDto(c.Field, c.OldValue, c.NewValue)).ToList(),
        result.Renamed.Select(r => new RenamedDto(r.Before, r.After)).ToList(),
        result.RenameWarning);
}

/// <summary>
/// A file renamed by 'cambiar_estado'.
/// </summary>
public sealed record RenamedDto(
    [property: JsonPropertyName("antes")] string Before,
    [property: JsonPropertyName("despues")] string After);

/// <summary>
/// 'crear_pieza': the new file and its metadata.
/// </summary>
public sealed record CreatedDocument(
    [property: JsonPropertyName("fichero")] string Path,
    [property: JsonPropertyName("serie")] string? Series,
    [property: JsonPropertyName("estado")] string? Status,
    [property: JsonPropertyName("fecha_prevista")] string? PlannedDate,
    [property: JsonPropertyName("hora")] string? Time,
    [property: JsonPropertyName("formato")] string? Format,
    [property: JsonPropertyName("depende_de")] IReadOnlyList<string> DependsOn)
{
    public static CreatedDocument Create(CreatedPiece created) => new(
        created.Path,
        created.Piece.Series,
        created.Piece.Status is { } status ? PieceStatusNames.ToName(status) : null,
        JsonOutput.Format(created.Piece.PlannedDate),
        created.Piece.Time?.ToString("HH:mm", CultureInfo.InvariantCulture),
        created.Piece.Format,
        created.Piece.DependsOn);
}
