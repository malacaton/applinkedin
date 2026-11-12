using System.Text;

namespace Ali.Core.Tests.Content;

/// <summary>
/// Temporary content folder for tests; deleted on dispose.
/// </summary>
internal sealed class TempContentFolder : IDisposable
{
    public TempContentFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "ali-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    /// <summary>
    /// Writes a UTF-8 file (no BOM) at a path relative to the root, creating folders as needed.
    /// </summary>
    public string Write(string relativePath, string content) => WriteBytes(relativePath, Encoding.UTF8.GetBytes(content));

    public string WriteBytes(string relativePath, byte[] content)
    {
        var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp folder must not fail the test.
        }
    }
}
