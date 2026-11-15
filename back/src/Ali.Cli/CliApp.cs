using System.CommandLine;
using Ali.Cli.Commands;
using Ali.Core;

namespace Ali.Cli;

/// <summary>
/// Entry point of the command line, with injectable output, environment and working directory for tests.
/// </summary>
public static class CliApp
{
    /// <summary>
    /// Parses <paramref name="args"/>, runs the matching command and returns the exit code.
    /// </summary>
    public static int Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        Func<string, string?> getEnvironmentVariable,
        string currentDirectory)
    {
        var context = new CliContext(output, error, getEnvironmentVariable, currentDirectory);

        var root = new RootCommand($"{AppInfo.Name}: planificador local de contenido para LinkedIn.")
        {
            ListCommand.Create(context),
        };

        var invocation = new InvocationConfiguration { Output = output, Error = error };
        return root.Parse(args).Invoke(invocation);
    }
}

/// <summary>
/// What a command needs from the outside world.
/// </summary>
public sealed record CliContext(
    TextWriter Output,
    TextWriter Error,
    Func<string, string?> GetEnvironmentVariable,
    string CurrentDirectory)
{
    public const string ContentFolderVariable = "ALI_CONTENIDO";

    public const int Success = 0;

    public const int Failure = 1;

    /// <summary>
    /// Content folder: explicit option, then ALI_CONTENIDO, then the current directory.
    /// </summary>
    public string ResolveContentFolder(string? option)
    {
        var chosen = !string.IsNullOrWhiteSpace(option)
            ? option
            : GetEnvironmentVariable(ContentFolderVariable) is { Length: > 0 } fromEnvironment
                && !string.IsNullOrWhiteSpace(fromEnvironment)
                ? fromEnvironment
                : CurrentDirectory;

        return Path.GetFullPath(chosen, CurrentDirectory);
    }

    public int Fail(string message)
    {
        Error.WriteLine($"error: {message}");
        return Failure;
    }
}
