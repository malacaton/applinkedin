using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ali.Core.Scheduling;

namespace Ali.Core.Content;

/// <summary>
/// What to create. Null or empty optional values are not written.
/// </summary>
public sealed record NewPiece
{
    public required string SeriesId { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// Explicit 'fecha_prevista'. Not compatible with <see cref="UseFirstFreeDate"/>.
    /// </summary>
    public DateOnly? PlannedDate { get; init; }

    /// <summary>
    /// Use the first free date of the series from <see cref="SearchFrom"/> as 'fecha_prevista'.
    /// </summary>
    public bool UseFirstFreeDate { get; init; }

    public DateOnly SearchFrom { get; init; }

    public string? Format { get; init; }

    public IReadOnlyList<string> DependsOn { get; init; } = [];

    /// <summary>
    /// 'hora'. When null, the time of the series cadence, if any.
    /// </summary>
    public TimeOnly? Time { get; init; }
}

/// <param name="Path">Path of the new file, relative to the content root, using '/'.</param>
/// <param name="Piece">Metadata of the new piece, as read back from the file content.</param>
/// <param name="Slot">Slot used with <see cref="NewPiece.UseFirstFreeDate"/>; otherwise null.</param>
public sealed record CreatedPiece(string Path, Piece Piece, Slot? Slot);

/// <summary>
/// The piece was not created. No file has been written.
/// </summary>
public sealed class PieceCreationException : Exception
{
    public PieceCreationException(string message)
        : base(message)
    {
    }

    public PieceCreationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Creates the .md file of a new piece with the series naming convention and a front matter block. Never overwrites.
/// </summary>
public sealed partial class PieceCreator
{
    private const string MarkdownExtension = ".md";
    private const string DateFormat = "yyyy-MM-dd";
    private const int MaxNumberDigits = 9;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static readonly HashSet<string> YamlReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "null", "true", "false", "yes", "no", "on", "off", "y", "n",
    };

    // Letters, digits and a few safe separators; starts with a letter or digit, so YAML reads it as plain text.
    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} _./-]*$")]
    private static partial Regex PlainScalar();

    /// <exception cref="ArgumentException">The root path is null, empty or blank.</exception>
    /// <exception cref="DirectoryNotFoundException">The content folder does not exist.</exception>
    /// <exception cref="PieceCreationException">A rule failed or the file already exists; nothing has been written.</exception>
    public CreatedPiece Create(string rootPath, SeriesConfig config, IReadOnlyList<Piece> pieces, NewPiece request)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("La carpeta de contenido es obligatoria.", nameof(rootPath));
        }

        var root = new DirectoryInfo(rootPath);
        if (!root.Exists)
        {
            throw new DirectoryNotFoundException($"No existe la carpeta de contenido: {root.FullName}");
        }

        var seriesId = request.SeriesId?.Trim() ?? string.Empty;
        var series = config.Find(seriesId)
            ?? throw new PieceCreationException(SlotFinder.UnknownSeriesMessage(config, seriesId));

        var title = SingleLine(request.Title ?? string.Empty);
        var slug = TitleSlug.From(title);
        if (slug.Length == 0)
        {
            throw new PieceCreationException("El título no contiene letras ni dígitos.");
        }

        if (request.PlannedDate is not null && request.UseFirstFreeDate)
        {
            throw new PieceCreationException("Usa --fecha o --fecha-libre, no las dos.");
        }

        var ordered = SlotFinder.Ordered(pieces);
        var slot = request.UseFirstFreeDate ? FirstFreeSlot(series, ordered, request.SearchFrom) : null;
        var date = slot?.Date ?? request.PlannedDate;

        var template = series.ResolvedFileName;
        if (template.UsesDate && date is null)
        {
            var token = template.Text.Contains("{" + FileNameTemplate.ShortDateToken + "}", StringComparison.Ordinal)
                ? FileNameTemplate.ShortDateToken
                : FileNameTemplate.DateToken;
            throw new PieceCreationException(
                $"La serie '{series.Id}' usa {{{token}}} en el nombre: indica --fecha o --fecha-libre.");
        }

        var dependsOn = DependsOn(request.DependsOn, slot);
        var format = string.IsNullOrWhiteSpace(request.Format) ? null : request.Format.Trim();
        var time = request.Time ?? series.Cadence?.Time;

        var number = series.Numbering == SeriesNumbering.Dependency
            ? DependencyNumber(series, config, ordered, dependsOn)
            : NextNumber(series, ordered);
        var fileName = template.Resolve(new FileNameValues(number, slug, TitleSlug.Original(title), date)) + MarkdownExtension;
        var relative = series.ResolvedFolder == Series.RootFolder ? fileName : $"{series.ResolvedFolder}/{fileName}";
        var fullPath = ResolveInside(root, relative);

        var content = BuildContent(series.Id, date, time, format, dependsOn, title);
        var piece = Verify(relative, content, series.Id, date, time, format, dependsOn);

        Write(relative, fullPath, content);
        return new CreatedPiece(relative, piece, slot);
    }

    private static string SingleLine(string text) =>
        string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim())).Trim();

    /// <summary>
    /// First slot of HU-05 whose date no piece of the series has yet, in any status (soft reservation).
    /// </summary>
    private static Slot FirstFreeSlot(Series series, List<Piece> pieces, DateOnly from)
    {
        if (!SlotFinder.HasRule(series))
        {
            throw new PieceCreationException($"La serie '{series.Id}' no tiene cadencia; no se pueden calcular huecos.");
        }

        var taken = pieces
            .Where(p => p.Series == series.Id && p.EffectiveDate is not null)
            .Select(p => p.EffectiveDate!.Value)
            .ToHashSet();

        return SlotFinder.Enumerate(series, pieces, from, to: null).FirstOrDefault(s => !taken.Contains(s.Date))
            ?? throw new PieceCreationException(
                $"No hay huecos libres para {series.Id} desde {from.ToString(DateFormat, CultureInfo.InvariantCulture)}.");
    }

    private static List<string> DependsOn(IReadOnlyList<string> requested, Slot? slot)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in requested.Select(v => v?.Trim()).Where(v => !string.IsNullOrEmpty(v)))
        {
            if (seen.Add(PieceReference.Normalize(value!)))
            {
                result.Add(value!);
            }
        }

        if (slot?.After is { } after)
        {
            var reference = PieceReference.Normalize(after.Path);
            if (seen.Add(reference))
            {
                result.Add(reference);
            }
        }

        return result;
    }

    /// <summary>
    /// Highest number of the series, plus one; 1 when there is none.
    /// </summary>
    private static int NextNumber(Series series, List<Piece> pieces) =>
        pieces
            .Where(p => p.Series == series.Id)
            .Select(p => ReadNumber(p, series))
            .Where(n => n is not null)
            .Select(n => n!.Value)
            .DefaultIfEmpty(0)
            .Max() + 1;

    /// <summary>
    /// Number of the first piece this one depends on (series with 'numeracion: dependencia').
    /// </summary>
    private static int DependencyNumber(Series series, SeriesConfig config, List<Piece> pieces, List<string> dependsOn)
    {
        if (dependsOn.Count == 0)
        {
            throw new PieceCreationException($"La serie '{series.Id}' numera por dependencia: indica --depende-de.");
        }

        var reference = PieceReference.Normalize(dependsOn[0]);
        var dependency = pieces.FirstOrDefault(
            p => string.Equals(PieceReference.Normalize(p.Path), reference, StringComparison.OrdinalIgnoreCase));
        var dependencySeries = dependency?.Series is { } id ? config.Find(id) : null;

        return (dependency is null ? null : ReadNumber(dependency, dependencySeries))
            ?? throw new PieceCreationException(
                $"No se puede leer el número de '{dependsOn[0]}', de la que depende la pieza.");
    }

    /// <summary>
    /// Number of an existing piece: read with the template of its series or, when the name does not fit, its leading digits.
    /// </summary>
    internal static int? ReadNumber(Piece piece, Series? series)
    {
        var name = piece.Path[(piece.Path.LastIndexOf('/') + 1)..];
        if (name.EndsWith(MarkdownExtension, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^MarkdownExtension.Length];
        }

        if (series?.ResolvedFileName.TryMatch(name, out var match) == true && match!.Number is { } number)
        {
            return number;
        }

        var digits = name.TakeWhile(char.IsAsciiDigit).Take(MaxNumberDigits).ToArray();
        return digits.Length > 0 ? int.Parse(digits, CultureInfo.InvariantCulture) : null;
    }

    private static string ResolveInside(DirectoryInfo root, string relative)
    {
        var rootFull = Path.TrimEndingDirectorySeparator(root.FullName);
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var fullPath = Path.GetFullPath(Path.Combine([rootFull, .. segments]));
        if (segments.Any(s => s is "." or ".." || s.StartsWith('.'))
            || !fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar, PathComparison))
        {
            throw new PieceCreationException("La pieza debe estar dentro de la carpeta de contenido.");
        }

        return fullPath;
    }

    private static string BuildContent(string seriesId, DateOnly? date, TimeOnly? time, string? format, List<string> dependsOn, string title)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append($"{MetadataParser.SeriesField}: {Scalar(seriesId)}\n");
        builder.Append($"{MetadataParser.StatusField}: {PieceStatusNames.ToName(PieceStatus.Idea)}\n");
        if (date is { } d)
        {
            builder.Append($"{MetadataParser.PlannedDateField}: {d.ToString(DateFormat, CultureInfo.InvariantCulture)}\n");
        }

        if (time is { } t)
        {
            builder.Append($"{MetadataParser.TimeField}: \"{t.ToString("HH:mm", CultureInfo.InvariantCulture)}\"\n");
        }

        if (format is not null)
        {
            builder.Append($"{MetadataParser.FormatField}: {Scalar(format)}\n");
        }

        if (dependsOn.Count > 0)
        {
            builder.Append($"{MetadataParser.DependsOnField}:\n");
            foreach (var dependency in dependsOn)
            {
                builder.Append($"  - {Scalar(dependency)}\n");
            }
        }

        builder.Append("---\n");
        builder.Append($"# {title}\n");
        return builder.ToString();
    }

    /// <summary>
    /// Plain YAML scalar when it is safe; otherwise a double-quoted one.
    /// </summary>
    private static string Scalar(string value)
    {
        if (PlainScalar().IsMatch(value) && !value.EndsWith(' ') && !YamlReservedWords.Contains(value)
            && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return value;
        }

        var escaped = value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    /// <summary>
    /// Safety net: the content must read back, with the catalog reader, as exactly the requested metadata.
    /// </summary>
    private static Piece Verify(
        string relative,
        string content,
        string seriesId,
        DateOnly? date,
        TimeOnly? time,
        string? format,
        List<string> dependsOn)
    {
        var frontMatter = FrontMatterReader.Read(new StringReader(content));
        var warnings = new List<CatalogWarning>();
        var piece = frontMatter.Kind == FrontMatterKind.Present
            ? MetadataParser.Parse(relative, frontMatter.Yaml, warnings)
            : null;

        if (piece is null
            || warnings.Count > 0
            || piece.Series != seriesId
            || piece.Status != PieceStatus.Idea
            || piece.PlannedDate != date
            || piece.Time != time
            || piece.Format != format
            || !piece.DependsOn.SequenceEqual(dependsOn))
        {
            throw new PieceCreationException(
                $"No se pudo crear '{relative}' de forma segura: el bloque generado no da los valores pedidos.");
        }

        return piece;
    }

    private static void Write(string relative, string fullPath, string content)
    {
        var conflict = new PieceCreationException($"Ya existe '{relative}'; no se sobrescribe.");
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            throw conflict;
        }

        FileStream stream;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException) when (File.Exists(fullPath))
        {
            // Created by someone else between the check and the creation.
            throw conflict;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PieceCreationException($"No se pudo crear '{relative}': {ex.Message}", ex);
        }

        try
        {
            using (stream)
            {
                stream.Write(Utf8.GetBytes(content));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The file is ours: do not leave it half written.
            TryDelete(fullPath);
            throw new PieceCreationException($"No se pudo crear '{relative}': {ex.Message}", ex);
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
            // Best effort.
        }
    }
}
