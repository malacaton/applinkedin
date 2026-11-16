namespace Ali.Core.Content;

/// <summary>
/// Values to write in a piece's front matter. A null property means "leave as is".
/// </summary>
public sealed record PieceChanges
{
    public PieceStatus? Status { get; init; }

    public DateOnly? PlannedDate { get; init; }

    public DateOnly? PublishedDate { get; init; }

    public string? Url { get; init; }

    public TimeOnly? Time { get; init; }

    public bool IsEmpty => Status is null && PlannedDate is null && PublishedDate is null && Url is null && Time is null;
}

/// <summary>
/// One field whose value changed, with both values formatted as written in the front matter.
/// </summary>
/// <param name="Field">Front matter key, e.g. "estado".</param>
/// <param name="OldValue">Previous value, or null when the field was absent or invalid.</param>
/// <param name="NewValue">Value written.</param>
public sealed record FieldChange(string Field, string? OldValue, string NewValue);

/// <param name="Path">Path relative to the content root, using '/' as separator.</param>
/// <param name="Before">Piece as it was read.</param>
/// <param name="After">Piece after the update (equal to <paramref name="Before"/> when nothing changed).</param>
/// <param name="Changes">Fields that changed; empty when the file was not written.</param>
public sealed record PieceUpdateResult(string Path, Piece Before, Piece After, IReadOnlyList<FieldChange> Changes)
{
    public bool HasChanges => Changes.Count > 0;

    /// <summary>
    /// Files renamed because the date in their name changed (the piece first, then its companions).
    /// </summary>
    public IReadOnlyList<RenamedFile> Renamed { get; init; } = [];

    /// <summary>
    /// Why the files were not renamed although the date in their name changed; null when not needed or done.
    /// </summary>
    public string? RenameWarning { get; init; }
}

/// <summary>
/// A file renamed by an update. Both paths are relative to the content root, using '/'.
/// </summary>
public sealed record RenamedFile(string Before, string After);

/// <summary>
/// The update was rejected. The file has not been modified.
/// </summary>
public sealed class PieceUpdateException : Exception
{
    public PieceUpdateException(string message)
        : base(message)
    {
    }

    public PieceUpdateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
