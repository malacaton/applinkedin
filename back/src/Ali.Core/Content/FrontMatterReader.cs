using System.Text;

namespace Ali.Core.Content;

internal enum FrontMatterKind
{
    None,
    Unclosed,
    Present,
}

/// <param name="Kind">Whether the file has a front matter block.</param>
/// <param name="Yaml">Block content, without the delimiter lines (only when <see cref="FrontMatterKind.Present"/>).</param>
internal sealed record FrontMatter(FrontMatterKind Kind, string Yaml = "");

/// <summary>
/// Extracts the YAML front matter from the beginning of a file without loading its body.
/// </summary>
internal static class FrontMatterReader
{
    private const string Delimiter = "---";
    private const string AlternativeEnd = "...";

    public static FrontMatter Read(string fullPath)
    {
        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        return Read(reader);
    }

    public static FrontMatter Read(TextReader reader)
    {
        var first = reader.ReadLine();
        if (first is null || first.TrimEnd() != Delimiter)
        {
            return new FrontMatter(FrontMatterKind.None);
        }

        var yaml = new StringBuilder();
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.TrimEnd();
            if (trimmed is Delimiter or AlternativeEnd)
            {
                return new FrontMatter(FrontMatterKind.Present, yaml.ToString());
            }

            yaml.Append(line).Append('\n');
        }

        return new FrontMatter(FrontMatterKind.Unclosed);
    }
}
