using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ali.Core.Content;
using Ali.Core.Scheduling;

namespace Ali.Cli.Output;

/// <summary>
/// Machine-readable slot list: 'serie', 'desde', 'huecos' and 'avisos'.
/// </summary>
internal static class JsonSlotsWriter
{
    private const string MismatchWarning = "La fecha no coincide con la cadencia de la serie.";

    public static void Write(TextWriter output, SlotSearch search, IReadOnlyList<CatalogWarning> warnings)
    {
        var document = new SlotsDocument(
            search.Series.Id,
            Format(search.From),
            search.Slots.Select(s => new SlotDto(
                Format(s.Date),
                SpanishDays.ToName(s.Date.DayOfWeek),
                s.After?.Path,
                s.SameDay.Select(p => p.Path).ToList(),
                s.CadenceMismatch ? MismatchWarning : null)).ToList(),
            warnings.Select(WarningDto.From).ToList());

        output.WriteLine(JsonSerializer.Serialize(document, JsonOutput.Options));
    }

    private static string Format(DateOnly date) => date.ToString(JsonOutput.DateFormat, CultureInfo.InvariantCulture);

    private sealed record SlotsDocument(
        [property: JsonPropertyName("serie")] string Series,
        [property: JsonPropertyName("desde")] string From,
        [property: JsonPropertyName("huecos")] IReadOnlyList<SlotDto> Slots,
        [property: JsonPropertyName("avisos")] IReadOnlyList<WarningDto> Warnings);

    private sealed record SlotDto(
        [property: JsonPropertyName("fecha")] string Date,
        [property: JsonPropertyName("dia")] string Day,
        [property: JsonPropertyName("tras")] string? After,
        [property: JsonPropertyName("coincide_con")] IReadOnlyList<string> SameDay,
        [property: JsonPropertyName("aviso")] string? Warning);
}
