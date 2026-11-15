namespace Ali.Core.Content;

/// <summary>
/// Lifecycle status of a piece. Stored in the YAML front matter with its Spanish name.
/// </summary>
public enum PieceStatus
{
    Idea,
    Draft,
    Ready,
    Scheduled,
    Published,
}

/// <summary>
/// Conversion between <see cref="PieceStatus"/> and the values written in the front matter.
/// </summary>
public static class PieceStatusNames
{
    private static readonly Dictionary<string, PieceStatus> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["idea"] = PieceStatus.Idea,
        ["borrador"] = PieceStatus.Draft,
        ["listo"] = PieceStatus.Ready,
        ["programado"] = PieceStatus.Scheduled,
        ["publicado"] = PieceStatus.Published,
    };

    /// <summary>
    /// Valid front matter values, in lifecycle order.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = ["idea", "borrador", "listo", "programado", "publicado"];

    public static bool TryParse(string? value, out PieceStatus status) =>
        ByName.TryGetValue(value?.Trim() ?? string.Empty, out status);

    public static string ToName(PieceStatus status) => status switch
    {
        PieceStatus.Idea => "idea",
        PieceStatus.Draft => "borrador",
        PieceStatus.Ready => "listo",
        PieceStatus.Scheduled => "programado",
        PieceStatus.Published => "publicado",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
