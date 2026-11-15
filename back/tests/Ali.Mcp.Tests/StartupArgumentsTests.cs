using Ali.Core.Tests.Content;

namespace Ali.Mcp.Tests;

public class StartupArgumentsTests : IDisposable
{
    private readonly TempContentFolder folder = new();

    public void Dispose() => folder.Dispose();

    private StartupResult Parse(Func<string, string?> environment, params string[] args) =>
        StartupArguments.Parse(args, environment, folder.Root);

    [Fact]
    public void Content_FromOption_SeriesDefaultsToContentFolder()
    {
        var result = Parse(_ => null, "--contenido", folder.Root);

        Assert.Null(result.Error);
        Assert.Equal(folder.Root, result.Settings!.ContentFolder);
        Assert.Equal(Path.Combine(folder.Root, "series.yaml"), result.Settings.SeriesFile);
    }

    [Fact]
    public void Content_FromEnvironment_AndRelativeSeriesOption()
    {
        var result = Parse(name => name == "ALI_CONTENIDO" ? folder.Root : null, "--series", "conf/series.yaml");

        Assert.Equal(folder.Root, result.Settings!.ContentFolder);
        Assert.Equal(Path.Combine(folder.Root, "conf", "series.yaml"), result.Settings.SeriesFile);
    }

    [Fact]
    public void Series_FromEnvironment()
    {
        var other = Path.Combine(folder.Root, "otra.yaml");

        var result = Parse(name => name == "ALI_SERIES" ? other : null, "--contenido", folder.Root);

        Assert.Equal(other, result.Settings!.SeriesFile);
    }

    [Fact]
    public void Content_IsRequired_EvenWithCurrentDirectory()
    {
        var result = Parse(_ => null);

        Assert.Null(result.Settings);
        Assert.StartsWith("Indica la carpeta de contenido con --contenido o con la variable ALI_CONTENIDO.", result.Error);
    }

    [Fact]
    public void Content_MustExist()
    {
        var missing = Path.Combine(folder.Root, "no-existe");

        Assert.Equal($"No existe la carpeta de contenido: {missing}", Parse(_ => null, "--contenido", missing).Error);
    }

    [Theory]
    [InlineData(new[] { "--hola" }, "Argumento desconocido: --hola.")]
    [InlineData(new[] { "--contenido" }, "Falta el valor de --contenido.")]
    public void InvalidArguments(string[] args, string start)
    {
        Assert.StartsWith(start, Parse(_ => null, args).Error);
    }
}
