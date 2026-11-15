using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ali.Core.Content;

namespace Ali.Cli.Output;

/// <summary>
/// Settings and shapes shared by every JSON output.
/// </summary>
internal static class JsonOutput
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,

        // Output goes to a terminal or another tool, never into HTML: keep accents readable.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public const string DateFormat = "yyyy-MM-dd";
}

/// <summary>
/// A catalog warning in JSON ('avisos').
/// </summary>
internal sealed record WarningDto(
    [property: JsonPropertyName("fichero")] string Path,
    [property: JsonPropertyName("codigo")] string Code,
    [property: JsonPropertyName("campo")] string? Field,
    [property: JsonPropertyName("motivo")] string Message)
{
    public static WarningDto From(CatalogWarning warning) =>
        new(warning.Path, warning.Code.ToString(), warning.Field, warning.Message);
}
