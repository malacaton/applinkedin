namespace Ali.Core.Content;

/// <summary>
/// Reads a content folder into a <see cref="Catalog"/>. Never modifies any file.
/// </summary>
public sealed class CatalogReader
{
    private const string MarkdownExtension = ".md";

    /// <summary>
    /// Reads every non-hidden .md file under <paramref name="rootPath"/>, recursively.
    /// </summary>
    /// <exception cref="ArgumentException">The path is null, empty or blank.</exception>
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    public Catalog Read(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("La carpeta de contenido es obligatoria.", nameof(rootPath));
        }

        var root = new DirectoryInfo(rootPath);
        if (!root.Exists)
        {
            throw new DirectoryNotFoundException($"No existe la carpeta de contenido: {root.FullName}");
        }

        var pieces = new List<Piece>();
        var warnings = new List<CatalogWarning>();

        foreach (var file in EnumerateMarkdownFiles(root, root, warnings))
        {
            pieces.Add(ReadPiece(file, RelativePath(root, file), warnings));
        }

        var ordered = pieces
            .OrderBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Path, StringComparer.Ordinal)
            .ToList();

        var pathOrder = ordered
            .Select((piece, index) => (piece.Path, index))
            .ToDictionary(x => x.Path, x => x.index, StringComparer.Ordinal);

        // Warnings follow the piece order; directory-level warnings (no piece) go last.
        var orderedWarnings = warnings
            .Select((warning, index) => (warning, index))
            .OrderBy(x => pathOrder.GetValueOrDefault(x.warning.Path, int.MaxValue))
            .ThenBy(x => x.index)
            .Select(x => x.warning)
            .ToList();

        return new Catalog(ordered, orderedWarnings);
    }

    private static Piece ReadPiece(FileInfo file, string path, List<CatalogWarning> warnings)
    {
        FrontMatter frontMatter;
        try
        {
            frontMatter = FrontMatterReader.Read(file.FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add(new CatalogWarning(
                path,
                CatalogWarningCode.FileUnreadable,
                null,
                $"No se pudo leer el fichero: {ex.Message}"));
            return new Piece { Path = path };
        }

        switch (frontMatter.Kind)
        {
            case FrontMatterKind.None:
                return new Piece { Path = path };
            case FrontMatterKind.Unclosed:
                warnings.Add(new CatalogWarning(
                    path,
                    CatalogWarningCode.FrontMatterUnclosed,
                    null,
                    "El bloque YAML empieza con '---' pero no tiene línea de cierre ('---' o '...')."));
                return new Piece { Path = path };
            default:
                return MetadataParser.Parse(path, frontMatter.Yaml, warnings);
        }
    }

    private static IEnumerable<FileInfo> EnumerateMarkdownFiles(
        DirectoryInfo root,
        DirectoryInfo directory,
        List<CatalogWarning> warnings)
    {
        FileSystemInfo[] entries;
        try
        {
            entries = directory.GetFileSystemInfos();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add(new CatalogWarning(
                RelativePath(root, directory),
                CatalogWarningCode.FileUnreadable,
                null,
                $"No se pudo leer la carpeta: {ex.Message}"));
            yield break;
        }

        foreach (var entry in entries)
        {
            if (IsSkipped(entry))
            {
                continue;
            }

            switch (entry)
            {
                case DirectoryInfo subdirectory:
                    foreach (var file in EnumerateMarkdownFiles(root, subdirectory, warnings))
                    {
                        yield return file;
                    }

                    break;
                case FileInfo file when file.Extension.Equals(MarkdownExtension, StringComparison.OrdinalIgnoreCase):
                    yield return file;
                    break;
            }
        }
    }

    private static bool IsSkipped(FileSystemInfo entry) =>
        entry.Name.StartsWith('.') || entry.Attributes.HasFlag(FileAttributes.ReparsePoint);

    private static string RelativePath(DirectoryInfo root, FileSystemInfo entry) =>
        Path.GetRelativePath(root.FullName, entry.FullName).Replace('\\', '/');
}
