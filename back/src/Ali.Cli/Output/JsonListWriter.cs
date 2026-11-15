using System.Text.Json;
using System.Text.Json.Serialization;
using Ali.Core.Content;

namespace Ali.Cli.Output;

/// <summary>
/// Machine-readable list: one JSON object with 'piezas' and 'avisos'. Field names match the YAML front matter.
/// </summary>
internal static class JsonListWriter
{
    public static void Write(TextWriter output, IReadOnlyList<Piece> pieces, IReadOnlyList<CatalogWarning> warnings)
    {
        var document = new ListDocument(
            pieces.Select(PieceDto.From).ToList(),
            warnings.Select(WarningDto.From).ToList());

        output.WriteLine(JsonSerializer.Serialize(document, JsonOutput.Options));
    }

    private sealed record ListDocument(
        [property: JsonPropertyName("piezas")] IReadOnlyList<PieceDto> Pieces,
        [property: JsonPropertyName("avisos")] IReadOnlyList<WarningDto> Warnings);

    private sealed record PieceDto(
        [property: JsonPropertyName("fichero")] string Path,
        [property: JsonPropertyName("serie")] string? Series,
        [property: JsonPropertyName("estado")] string? Status,
        [property: JsonPropertyName("fecha")] string? Date,
        [property: JsonPropertyName("fecha_prevista")] string? PlannedDate,
        [property: JsonPropertyName("fecha_publicada")] string? PublishedDate,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("depende_de")] IReadOnlyList<string> DependsOn,
        [property: JsonPropertyName("formato")] string? Format)
    {
        public static PieceDto From(Piece piece) => new(
            piece.Path,
            piece.Series,
            piece.Status is { } status ? PieceStatusNames.ToName(status) : null,
            FormatDate(piece.EffectiveDate),
            FormatDate(piece.PlannedDate),
            FormatDate(piece.PublishedDate),
            piece.Url,
            piece.DependsOn,
            piece.Format);

        private static string? FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd");
    }
}
