using System.Globalization;
using Ali.Core.Scheduling;

namespace Ali.Cli.Output;

/// <summary>
/// Human-readable calendar report: a summary line and an aligned table.
/// </summary>
internal static class TextValidationWriter
{
    private const string Missing = "—";
    private const string ColumnGap = "  ";
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly string[] Headers = ["FECHA", "TIPO", "SERIE", "PIEZAS", "DETALLE"];

    public static void Write(TextWriter output, CalendarReport report)
    {
        var range = $"del {Format(report.From)} al {Format(report.To)}";
        if (report.Problems.Count == 0)
        {
            output.WriteLine($"Sin problemas {range}.");
            return;
        }

        var total = report.Problems.Count == 1 ? "1 problema" : $"{report.Problems.Count} problemas";
        output.WriteLine($"Calendario {range}: {total}");
        output.WriteLine();

        var rows = report.Problems
            .Select(p => new[]
            {
                p.Date is { } date ? Format(date) : Missing,
                ProblemKindNames.ToName(p.Kind),
                p.Series ?? Missing,
                p.Pieces.Count == 0 ? Missing : string.Join(", ", p.Pieces),
                p.Message,
            })
            .ToList();

        var widths = Enumerable.Range(0, Headers.Length)
            .Select(column => rows.Select(r => r[column].Length).Append(Headers[column].Length).Max())
            .ToArray();

        output.WriteLine(FormatRow(Headers, widths));
        foreach (var row in rows)
        {
            output.WriteLine(FormatRow(row, widths));
        }
    }

    private static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static string FormatRow(string[] cells, int[] widths)
    {
        // The last column is not padded, to avoid trailing spaces.
        var padded = cells.Select((cell, i) => i == cells.Length - 1 ? cell : cell.PadRight(widths[i]));
        return string.Join(ColumnGap, padded);
    }
}
