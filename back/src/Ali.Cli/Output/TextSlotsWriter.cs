using System.Globalization;
using Ali.Core.Scheduling;

namespace Ali.Cli.Output;

/// <summary>
/// Human-readable slot list: a header with the series rule and an aligned table.
/// </summary>
internal static class TextSlotsWriter
{
    private const string Missing = "—";
    private const string ColumnGap = "  ";
    private const string DateFormat = "yyyy-MM-dd";
    private const string MismatchWarning = "La fecha no coincide con la cadencia de la serie.";

    public static void Write(TextWriter output, SlotSearch search)
    {
        var from = search.From.ToString(DateFormat, CultureInfo.InvariantCulture);
        if (search.Slots.Count == 0)
        {
            output.WriteLine($"No hay huecos pendientes para {search.Series.Id} desde {from}.");
            return;
        }

        output.WriteLine($"Huecos de {search.Series.Id} ({DescribeRule(search.Series)}) desde {from}:");
        output.WriteLine();

        var withAfter = search.Series.After is not null;
        var withWarning = search.Slots.Any(s => s.CadenceMismatch);

        var headers = new List<string> { "FECHA", "DÍA" };
        if (withAfter)
        {
            headers.Add("TRAS");
        }

        headers.Add("COINCIDE CON");
        if (withWarning)
        {
            headers.Add("AVISO");
        }

        var rows = search.Slots.Select(slot =>
        {
            var row = new List<string>
            {
                slot.Date.ToString(DateFormat, CultureInfo.InvariantCulture),
                SpanishDays.ToName(slot.Date.DayOfWeek),
            };
            if (withAfter)
            {
                row.Add(slot.After?.Path ?? Missing);
            }

            row.Add(slot.SameDay.Count == 0 ? Missing : string.Join(", ", slot.SameDay.Select(p => p.Path)));
            if (withWarning)
            {
                row.Add(slot.CadenceMismatch ? MismatchWarning : Missing);
            }

            return row.ToArray();
        }).ToList();

        var widths = Enumerable.Range(0, headers.Count)
            .Select(column => rows.Select(r => r[column].Length).Append(headers[column].Length).Max())
            .ToArray();

        output.WriteLine(FormatRow([.. headers], widths));
        foreach (var row in rows)
        {
            output.WriteLine(FormatRow(row, widths));
        }
    }

    public static string DescribeRule(Series series)
    {
        var parts = new List<string>();
        if (series.After is { } link)
        {
            parts.Add(link.Days == 1 ? $"1 día después de {link.SeriesId}" : $"{link.Days} días después de {link.SeriesId}");
        }

        if (series.Cadence is { } cadence)
        {
            var days = cadence.Days.Select(SpanishDays.ToName).ToList();
            var dayList = days.Count == 1 ? days[0] : string.Join(", ", days[..^1]) + " y " + days[^1];
            var frequency = cadence.Frequency == CadenceFrequency.Weekly ? "semanal" : "quincenal";
            parts.Add(series.After is null ? $"{dayList}, {frequency}" : $"cadencia {dayList}, {frequency}");
        }

        return string.Join("; ", parts);
    }

    private static string FormatRow(string[] cells, int[] widths)
    {
        // The last column is not padded, to avoid trailing spaces.
        var padded = cells.Select((cell, i) => i == cells.Length - 1 ? cell : cell.PadRight(widths[i]));
        return string.Join(ColumnGap, padded);
    }
}
