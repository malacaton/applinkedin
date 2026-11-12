namespace Ali.Core.Content;

/// <summary>
/// A content file and the metadata read from its front matter.
/// </summary>
public sealed record Piece
{
    /// <summary>
    /// Path relative to the content root, using '/' as separator.
    /// </summary>
    public required string Path { get; init; }

    public string? Series { get; init; }

    public PieceStatus? Status { get; init; }

    public DateOnly? PlannedDate { get; init; }

    public DateOnly? PublishedDate { get; init; }

    public string? Url { get; init; }

    /// <summary>
    /// Relative paths of the pieces this one depends on (with or without the .md extension).
    /// </summary>
    public IReadOnlyList<string> DependsOn { get; init; } = [];

    public string? Format { get; init; }

    /// <summary>
    /// True when the file starts with a closed and valid YAML front matter block.
    /// </summary>
    public bool HasFrontMatter { get; init; }

    /// <summary>
    /// A piece without series is "unclassified".
    /// </summary>
    public bool IsUnclassified => Series is null;
}
