using System.Reflection;

namespace Ali.Core;

/// <summary>
/// General information about the application.
/// </summary>
public static class AppInfo
{
    public const string Name = "appLinkedIn";

    /// <summary>
    /// Informational version of the core assembly (without the source revision suffix).
    /// </summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var version = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        var plusIndex = version.IndexOf('+');
        return plusIndex >= 0 ? version[..plusIndex] : version;
    }
}
