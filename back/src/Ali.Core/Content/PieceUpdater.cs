using System.Globalization;
using System.Text;

namespace Ali.Core.Content;

/// <summary>
/// Changes the status, dates and URL of a piece by editing only the lines of its YAML front matter.
/// The body is copied byte for byte and the file is replaced atomically.
/// </summary>
public sealed class PieceUpdater
{
    private const string MarkdownExtension = ".md";
    private const string DateFormat = "yyyy-MM-dd";
    private const string Delimiter = "---";
    private const string AlternativeEnd = "...";
    private const string DefaultNewLine = "\n";

    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>
    /// Applies <paramref name="changes"/> to the piece at <paramref name="piecePath"/>, relative to <paramref name="rootPath"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The root path is null, empty or blank.</exception>
    /// <exception cref="DirectoryNotFoundException">The content folder does not exist.</exception>
    /// <exception cref="PieceUpdateException">A validation rule failed; the file has not been modified.</exception>
    public PieceUpdateResult Update(string rootPath, string piecePath, PieceChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("La carpeta de contenido es obligatoria.", nameof(rootPath));
        }

        var root = new DirectoryInfo(rootPath);
        if (!root.Exists)
        {
            throw new DirectoryNotFoundException($"No existe la carpeta de contenido: {root.FullName}");
        }

        if (changes.IsEmpty)
        {
            throw new PieceUpdateException(
                "Indica al menos un campo que cambiar: --estado, --fecha-prevista, --fecha-publicada o --url.");
        }

        if (changes.Url is { } url && !IsValidUrl(url))
        {
            throw new PieceUpdateException($"URL '{url}' no válida: debe empezar por http:// o https://.");
        }

        var (relative, fullPath) = ResolvePiece(root, piecePath);
        var original = ReadAllBytes(relative, fullPath);
        var document = FrontMatterDocument.Parse(original);

        var before = ReadCurrent(relative, document);

        if (changes.Status == PieceStatus.Published && (changes.PublishedDate ?? before.PublishedDate) is null)
        {
            throw new PieceUpdateException(
                "Para marcar como publicado hace falta --fecha-publicada (la pieza no tiene fecha_publicada).");
        }

        var fieldChanges = Compare(before, changes);
        if (fieldChanges.Count == 0)
        {
            return new PieceUpdateResult(relative, before, before, []);
        }

        var expected = before with
        {
            Status = changes.Status ?? before.Status,
            PlannedDate = changes.PlannedDate ?? before.PlannedDate,
            PublishedDate = changes.PublishedDate ?? before.PublishedDate,
            Url = changes.Url ?? before.Url,
            HasFrontMatter = true,
        };

        var updated = document.Rewrite(fieldChanges);
        var after = Verify(relative, updated, expected);

        Write(relative, fullPath, original, updated);

        return new PieceUpdateResult(relative, before, after, fieldChanges);
    }

    private static bool IsValidUrl(string url) =>
        !url.Any(char.IsWhiteSpace)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static (string Relative, string FullPath) ResolvePiece(DirectoryInfo root, string piecePath)
    {
        var input = piecePath?.Trim() ?? string.Empty;
        if (input.Length == 0)
        {
            throw new PieceUpdateException("Indica la pieza que cambiar.");
        }

        var rootFull = Path.TrimEndingDirectorySeparator(root.FullName);
        var outside = new PieceUpdateException("La pieza debe estar dentro de la carpeta de contenido.");

        var relative = Path.IsPathRooted(input)
            ? Path.GetRelativePath(rootFull, Path.GetFullPath(input))
            : input;
        if (Path.IsPathRooted(relative))
        {
            throw outside;
        }

        relative = relative.Replace('\\', '/');
        if (!relative.EndsWith(MarkdownExtension, StringComparison.OrdinalIgnoreCase))
        {
            relative += MarkdownExtension;
        }

        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(s => s != ".").ToArray();
        if (segments.Contains(".."))
        {
            throw outside;
        }

        relative = string.Join('/', segments);
        var fullPath = Path.GetFullPath(Path.Combine([rootFull, .. segments]));
        if (!fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar, PathComparison))
        {
            throw outside;
        }

        // Hidden entries and links are not part of the catalog, so they are not pieces either.
        var notFound = new PieceUpdateException($"No existe la pieza '{relative}' en {rootFull}.");
        if (segments.Any(s => s.StartsWith('.') || s.IndexOfAny(['*', '?']) >= 0)
            || !File.Exists(fullPath)
            || File.GetAttributes(fullPath).HasFlag(FileAttributes.ReparsePoint))
        {
            throw notFound;
        }

        // Use the names as they are on disk: replacing the file must not change its casing.
        var actual = rootFull;
        var actualSegments = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            var match = Directory.EnumerateFileSystemEntries(actual, segment)
                .Select(Path.GetFileName)
                .OrderBy(name => string.Equals(name, segment, StringComparison.Ordinal) ? 0 : 1)
                .FirstOrDefault() ?? throw notFound;
            actualSegments.Add(match);
            actual = Path.Combine(actual, match);
        }

        return (string.Join('/', actualSegments), actual);
    }

    private static byte[] ReadAllBytes(string relative, string fullPath)
    {
        try
        {
            return File.ReadAllBytes(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PieceUpdateException($"No se pudo leer '{relative}': {ex.Message}", ex);
        }
    }

    private static Piece ReadCurrent(string relative, FrontMatterDocument document)
    {
        switch (document.Kind)
        {
            case FrontMatterKind.None:
                return new Piece { Path = relative };
            case FrontMatterKind.Unclosed:
                throw InvalidBlock(relative, "empieza con '---' pero no tiene línea de cierre");
        }

        var warnings = new List<CatalogWarning>();
        var piece = MetadataParser.Parse(relative, document.Yaml, warnings);
        if (warnings.FirstOrDefault(IsBlockWarning) is { } blockWarning)
        {
            throw InvalidBlock(relative, blockWarning.Message.TrimEnd('.'));
        }

        return piece;
    }

    private static bool IsBlockWarning(CatalogWarning warning) =>
        warning.Code is CatalogWarningCode.FrontMatterInvalid or CatalogWarningCode.FrontMatterNotMapping;

    private static PieceUpdateException InvalidBlock(string relative, string reason) =>
        new($"El bloque YAML de '{relative}' no es válido ({reason}); corrígelo a mano antes de cambiarlo.");

    /// <summary>
    /// Fields whose requested value differs from the current one, in the order they are written.
    /// </summary>
    private static List<FieldChange> Compare(Piece before, PieceChanges changes)
    {
        var result = new List<FieldChange>();

        if (changes.Status is { } status && before.Status != status)
        {
            result.Add(new FieldChange(
                MetadataParser.StatusField,
                before.Status is { } old ? PieceStatusNames.ToName(old) : null,
                PieceStatusNames.ToName(status)));
        }

        if (changes.PlannedDate is { } planned && before.PlannedDate != planned)
        {
            result.Add(new FieldChange(MetadataParser.PlannedDateField, FormatDate(before.PlannedDate), FormatDate(planned)!));
        }

        if (changes.PublishedDate is { } published && before.PublishedDate != published)
        {
            result.Add(new FieldChange(MetadataParser.PublishedDateField, FormatDate(before.PublishedDate), FormatDate(published)!));
        }

        if (changes.Url is { } url && !string.Equals(before.Url, url, StringComparison.Ordinal))
        {
            result.Add(new FieldChange(MetadataParser.UrlField, before.Url, url));
        }

        return result;
    }

    private static string? FormatDate(DateOnly? date) =>
        date?.ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Safety net: reads the new content with the catalog reader and checks it gives exactly the expected metadata.
    /// </summary>
    private static Piece Verify(string relative, byte[] updated, Piece expected)
    {
        using var reader = new StreamReader(new MemoryStream(updated), Utf8, detectEncodingFromByteOrderMarks: true);
        var frontMatter = FrontMatterReader.Read(reader);

        var warnings = new List<CatalogWarning>();
        var after = frontMatter.Kind == FrontMatterKind.Present
            ? MetadataParser.Parse(relative, frontMatter.Yaml, warnings)
            : null;

        if (after is null || warnings.Any(IsBlockWarning) || !SameMetadata(after, expected))
        {
            throw new PieceUpdateException(
                $"No se pudo actualizar '{relative}' de forma segura: el bloque resultante no da los valores pedidos. El fichero no se ha modificado.");
        }

        return after;
    }

    private static bool SameMetadata(Piece a, Piece b) =>
        a.HasFrontMatter == b.HasFrontMatter
        && a.Series == b.Series
        && a.Status == b.Status
        && a.PlannedDate == b.PlannedDate
        && a.PublishedDate == b.PublishedDate
        && a.Url == b.Url
        && a.Format == b.Format
        && a.DependsOn.SequenceEqual(b.DependsOn);

    private static void Write(string relative, string fullPath, byte[] original, byte[] updated)
    {
        var directory = Path.GetDirectoryName(fullPath)!;
        var temp = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            if (!File.ReadAllBytes(fullPath).AsSpan().SequenceEqual(original))
            {
                throw new PieceUpdateException(
                    $"El fichero '{relative}' ha cambiado mientras se actualizaba; vuelve a intentarlo.");
            }

            File.WriteAllBytes(temp, updated);
            File.Move(temp, fullPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            throw new PieceUpdateException($"No se pudo escribir '{relative}': {ex.Message}", ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the original file is untouched either way.
        }
    }

    /// <summary>
    /// A line of the file: its original bytes and its decoded text without the line break.
    /// </summary>
    private sealed record Line(string Text, string Terminator, byte[] Raw)
    {
        public static Line Create(string text, string terminator) =>
            new(text, terminator, Utf8.GetBytes(text + terminator));
    }

    /// <summary>
    /// A Markdown file split into BOM, front matter lines and raw body bytes.
    /// </summary>
    private sealed class FrontMatterDocument
    {
        private byte[] content = [];
        private bool hasBom;
        private string newLine = DefaultNewLine;
        private Line? opening;
        private Line? closing;
        private readonly List<Line> header = [];
        private int bodyStart;

        public FrontMatterKind Kind { get; private set; }

        /// <summary>
        /// YAML text of the block, as <see cref="FrontMatterReader"/> builds it.
        /// </summary>
        public string Yaml => string.Concat(header.Select(l => l.Text + "\n"));

        public static FrontMatterDocument Parse(byte[] content)
        {
            var document = new FrontMatterDocument { content = content };
            document.hasBom = content.AsSpan().StartsWith(Bom);

            var start = document.hasBom ? Bom.Length : 0;
            document.newLine = DetectNewLine(content, start);
            document.bodyStart = start;

            var position = start;
            var first = NextLine(content, ref position);
            if (first is null || first.Text.TrimEnd() != Delimiter)
            {
                document.Kind = FrontMatterKind.None;
                return document;
            }

            document.opening = first;
            while (NextLine(content, ref position) is { } line)
            {
                if (line.Text.TrimEnd() is Delimiter or AlternativeEnd)
                {
                    document.closing = line;
                    document.bodyStart = position;
                    document.Kind = FrontMatterKind.Present;
                    return document;
                }

                document.header.Add(line);
            }

            document.Kind = FrontMatterKind.Unclosed;
            return document;
        }

        /// <summary>
        /// Builds the new file content with <paramref name="changes"/> applied to the front matter.
        /// </summary>
        public byte[] Rewrite(IReadOnlyList<FieldChange> changes)
        {
            var lines = new List<Line>(header);
            foreach (var change in changes)
            {
                var index = lines.FindIndex(l => IsKeyLine(l.Text, change.Field));
                if (index < 0)
                {
                    lines.Add(Line.Create($"{change.Field}: {change.NewValue}", newLine));
                    continue;
                }

                var end = index + 1;
                while (end < lines.Count && IsContinuation(lines, end))
                {
                    end++;
                }

                var replacement = Line.Create($"{change.Field}: {change.NewValue}", lines[index].Terminator);
                lines.RemoveRange(index, end - index);
                lines.Insert(index, replacement);
            }

            using var output = new MemoryStream(content.Length + 256);
            if (hasBom)
            {
                output.Write(Bom);
            }

            output.Write((opening ?? Line.Create(Delimiter, newLine)).Raw);
            foreach (var line in lines)
            {
                output.Write(line.Raw);
            }

            output.Write((closing ?? Line.Create(Delimiter, newLine)).Raw);
            output.Write(content.AsSpan(bodyStart));
            return output.ToArray();
        }

        private static bool IsKeyLine(string text, string field)
        {
            if (!text.StartsWith(field, StringComparison.Ordinal))
            {
                return false;
            }

            var rest = text.AsSpan(field.Length).TrimStart(" \t");
            return rest.Length > 0 && rest[0] == ':' && (rest.Length == 1 || char.IsWhiteSpace(rest[1]));
        }

        /// <summary>
        /// Indented lines and sequence items belong to the previous key; so do blank lines followed by one of them.
        /// </summary>
        private static bool IsContinuation(List<Line> lines, int index)
        {
            for (var i = index; i < lines.Count; i++)
            {
                var text = lines[i].Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                return text[0] is ' ' or '\t' or '-';
            }

            return false;
        }

        private static string DetectNewLine(byte[] content, int start)
        {
            var index = Array.IndexOf(content, (byte)'\n', start);
            if (index < 0)
            {
                return DefaultNewLine;
            }

            return index > start && content[index - 1] == (byte)'\r' ? "\r\n" : "\n";
        }

        private static Line? NextLine(byte[] content, ref int position)
        {
            if (position >= content.Length)
            {
                return null;
            }

            var start = position;
            var newLineIndex = Array.IndexOf(content, (byte)'\n', start);
            var end = newLineIndex < 0 ? content.Length : newLineIndex + 1;
            var textEnd = newLineIndex < 0
                ? content.Length
                : newLineIndex > start && content[newLineIndex - 1] == (byte)'\r' ? newLineIndex - 1 : newLineIndex;

            position = end;
            return new Line(
                Utf8.GetString(content, start, textEnd - start),
                Utf8.GetString(content, textEnd, end - textEnd),
                content[start..end]);
        }
    }
}
