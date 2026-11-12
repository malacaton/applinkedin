namespace Ali.Core.Content;

/// <summary>
/// Result of reading a content folder: the pieces and, separately, the warnings found.
/// </summary>
public sealed record Catalog(IReadOnlyList<Piece> Pieces, IReadOnlyList<CatalogWarning> Warnings);

/// <summary>
/// A non-blocking problem found while reading a file.
/// </summary>
/// <param name="Path">Path relative to the content root, using '/' as separator.</param>
/// <param name="Code">Kind of problem.</param>
/// <param name="Field">Front matter field involved, when there is one.</param>
/// <param name="Message">Human-readable reason, in Spanish.</param>
public sealed record CatalogWarning(string Path, CatalogWarningCode Code, string? Field, string Message);

public enum CatalogWarningCode
{
    FrontMatterUnclosed,
    FrontMatterInvalid,
    FrontMatterNotMapping,
    InvalidStatus,
    InvalidDate,
    InvalidFieldType,
    FileUnreadable,
}
