using Ali.Core.Input;

namespace Ali.Mcp;

/// <summary>
/// Where the server reads and writes. Fixed at startup.
/// </summary>
/// <param name="ContentFolder">Full path of the content folder; it existed at startup.</param>
/// <param name="SeriesFile">Full path of the series file; it may not exist (only the tools that need it fail).</param>
public sealed record ServerSettings(string ContentFolder, string SeriesFile);

/// <summary>
/// Result of reading the command line: settings, or the reason the server cannot start.
/// </summary>
public sealed record StartupResult(ServerSettings? Settings, string? Error);

/// <summary>
/// Reads 'ali-mcp --contenido &lt;carpeta&gt; [--series &lt;fichero&gt;]'. Unlike the CLI, the current folder is never used
/// as content folder: AI clients start the server from an unpredictable folder.
/// </summary>
public static class StartupArguments
{
    public const string Usage = "Uso: ali-mcp --contenido <carpeta> [--series <fichero>]";

    private const string ContentOption = "--contenido";
    private const string SeriesOption = "--series";

    public static StartupResult Parse(string[] args, Func<string, string?> getEnvironmentVariable, string currentDirectory)
    {
        string? content = null;
        string? series = null;

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (name is not (ContentOption or SeriesOption))
            {
                return Fail($"Argumento desconocido: {name}. {Usage}");
            }

            if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                return Fail($"Falta el valor de {name}. {Usage}");
            }

            var value = args[++i];
            if (name == ContentOption)
            {
                content = value;
            }
            else
            {
                series = value;
            }
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            content = getEnvironmentVariable(ContentLocation.ContentFolderVariable);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return Fail($"Indica la carpeta de contenido con {ContentOption} o con la variable {ContentLocation.ContentFolderVariable}. {Usage}");
        }

        var contentFolder = Path.GetFullPath(content, currentDirectory);
        if (!Directory.Exists(contentFolder))
        {
            return Fail($"No existe la carpeta de contenido: {contentFolder}");
        }

        var seriesFile = ContentLocation.ResolveSeriesFile(series, getEnvironmentVariable, contentFolder, currentDirectory);
        return new StartupResult(new ServerSettings(contentFolder, seriesFile), null);
    }

    private static StartupResult Fail(string error) => new(null, error);
}
