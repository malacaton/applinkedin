namespace Ali.Core.Scheduling;

/// <summary>
/// One problem in the series configuration file.
/// </summary>
/// <param name="Series">Series label ("Serie 'id'" or "Serie n.º N"); null for file-level errors.</param>
/// <param name="Field">Field path, such as 'cadencia.dias'; null when not field-specific.</param>
/// <param name="Reason">What is wrong, in Spanish.</param>
public sealed record SeriesConfigError(string? Series, string? Field, string Reason)
{
    public override string ToString() => (Series, Field) switch
    {
        (null, null) => Reason,
        (null, _) => $"Campo '{Field}': {Reason}",
        (_, null) => $"{Series}: {Reason}",
        _ => $"{Series}, campo '{Field}': {Reason}",
    };
}

/// <summary>
/// The series configuration could not be loaded. <see cref="Errors"/> lists every problem found.
/// </summary>
public sealed class SeriesConfigException : Exception
{
    public SeriesConfigException(string path, IReadOnlyList<SeriesConfigError> errors)
        : base(BuildMessage(path, errors))
    {
        Path = path;
        Errors = errors;
    }

    public string Path { get; }

    public IReadOnlyList<SeriesConfigError> Errors { get; }

    private static string BuildMessage(string path, IReadOnlyList<SeriesConfigError> errors)
    {
        var header = errors.Count == 1
            ? $"El fichero de series '{path}' tiene 1 error:"
            : $"El fichero de series '{path}' tiene {errors.Count} errores:";
        return header + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "- " + e));
    }
}
