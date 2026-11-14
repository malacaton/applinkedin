using Ali.Core.Content;

namespace Ali.Cli.Output;

/// <summary>
/// Human-readable list: aligned columns, a total line, and warnings on a separate writer.
/// </summary>
internal static class TextListWriter
{
    private const string Missing = "—";
    private const string ColumnGap = "  ";

    private static readonly string[] Headers = ["FECHA", "ESTADO", "SERIE", "FICHERO"];

    public static void Write(TextWriter output, IReadOnlyList<Piece> pieces)
    {
        if (pieces.Count == 0)
        {
            output.WriteLine("No hay piezas que cumplan los filtros.");
            return;
        }

        var rows = pieces
            .Select(p => new[]
            {
                p.EffectiveDate?.ToString("yyyy-MM-dd") ?? Missing,
                p.Status is { } status ? PieceStatusNames.ToName(status) : Missing,
                p.Series ?? Missing,
                p.Path,
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

        output.WriteLine();
        output.WriteLine(pieces.Count == 1 ? "1 pieza" : $"{pieces.Count} piezas");
    }

    public static void WriteWarnings(TextWriter error, IReadOnlyList<CatalogWarning> warnings)
    {
        foreach (var warning in warnings)
        {
            error.WriteLine($"aviso: {warning.Path}: {warning.Message}");
        }
    }

    private static string FormatRow(string[] cells, int[] widths)
    {
        // The last column is not padded, to avoid trailing spaces.
        var padded = cells.Select((cell, i) => i == cells.Length - 1 ? cell : cell.PadRight(widths[i]));
        return string.Join(ColumnGap, padded);
    }
}
