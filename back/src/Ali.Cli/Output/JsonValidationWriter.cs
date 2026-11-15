using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ali.Core.Content;
using Ali.Core.Scheduling;

namespace Ali.Cli.Output;

/// <summary>
/// Machine-readable calendar report: 'desde', 'hasta', 'problemas' and 'avisos'.
/// </summary>
internal static class JsonValidationWriter
{
    public static void Write(TextWriter output, CalendarReport report, IReadOnlyList<CatalogWarning> warnings)
    {
        var document = new ReportDocument(
            Format(report.From),
            Format(report.To),
            report.Problems.Select(p => new ProblemDto(
                ProblemKindNames.ToName(p.Kind),
                p.Date is { } date ? Format(date) : null,
                p.Series,
                p.Pieces,
                p.Message)).ToList(),
            warnings.Select(WarningDto.From).ToList());

        output.WriteLine(JsonSerializer.Serialize(document, JsonOutput.Options));
    }

    private static string Format(DateOnly date) => date.ToString(JsonOutput.DateFormat, CultureInfo.InvariantCulture);

    private sealed record ReportDocument(
        [property: JsonPropertyName("desde")] string From,
        [property: JsonPropertyName("hasta")] string To,
        [property: JsonPropertyName("problemas")] IReadOnlyList<ProblemDto> Problems,
        [property: JsonPropertyName("avisos")] IReadOnlyList<WarningDto> Warnings);

    private sealed record ProblemDto(
        [property: JsonPropertyName("tipo")] string Kind,
        [property: JsonPropertyName("fecha")] string? Date,
        [property: JsonPropertyName("serie")] string? Series,
        [property: JsonPropertyName("piezas")] IReadOnlyList<string> Pieces,
        [property: JsonPropertyName("detalle")] string Message);
}
