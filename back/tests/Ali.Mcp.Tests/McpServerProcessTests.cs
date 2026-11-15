using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Ali.Mcp.Tests;

/// <summary>
/// Starts the real 'ali-mcp' process and talks to it over stdio.
/// </summary>
public class McpServerProcessTests : IDisposable
{
    private static readonly string ServerDll = Path.Combine(AppContext.BaseDirectory, "ali-mcp.dll");

    private readonly McpTestContent content = new();

    public void Dispose() => content.Dispose();

    private Task<McpClient> ConnectAsync() =>
        McpClient.CreateAsync(
            new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "ali-mcp",
                Command = "dotnet",
                Arguments = [ServerDll, "--contenido", content.Root],
            }),
            cancellationToken: TestContext.Current.CancellationToken);

    private static string Text(CallToolResult result) => Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    // ---------- CA-1: herramientas ----------

    [Fact]
    public async Task ListTools_ExposesFiveToolsWithDescriptionsAndAnnotations()
    {
        await using var client = await ConnectAsync();

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            ["buscar_huecos", "cambiar_estado", "crear_pieza", "listar_piezas", "validar_calendario"],
            tools.Select(t => t.Name).Order(StringComparer.Ordinal));

        foreach (var tool in tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description), tool.Name);
            var annotations = tool.ProtocolTool.Annotations!;
            Assert.False(annotations.DestructiveHint, tool.Name);
            Assert.False(annotations.OpenWorldHint, tool.Name);

            var properties = tool.JsonSchema.GetProperty("properties");
            foreach (var parameter in properties.EnumerateObject())
            {
                Assert.True(parameter.Value.TryGetProperty("description", out _), $"{tool.Name}.{parameter.Name}");
            }
        }

        var byName = tools.ToDictionary(t => t.Name);
        Assert.True(byName["listar_piezas"].ProtocolTool.Annotations!.ReadOnlyHint);
        Assert.True(byName["buscar_huecos"].ProtocolTool.Annotations!.ReadOnlyHint);
        Assert.True(byName["validar_calendario"].ProtocolTool.Annotations!.ReadOnlyHint);
        Assert.False(byName["cambiar_estado"].ProtocolTool.Annotations!.ReadOnlyHint);
        Assert.True(byName["cambiar_estado"].ProtocolTool.Annotations!.IdempotentHint);
        Assert.False(byName["crear_pieza"].ProtocolTool.Annotations!.ReadOnlyHint);
        Assert.False(byName["crear_pieza"].ProtocolTool.Annotations!.IdempotentHint);

        Assert.Equal(
            ["desde", "estado", "hasta", "serie", "sin_clasificar"],
            byName["listar_piezas"].JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    // ---------- CA-2 y CA-4: llamadas reales ----------

    [Fact]
    public async Task CallTool_ReturnsSameJsonAsCli()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync(
            "listar_piezas",
            new Dictionary<string, object?> { ["serie"] = new[] { "casos-reales" } },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(content.Cli("listar", "--json", "-s", "casos-reales"), Text(result));
    }

    [Fact]
    public async Task CallTool_DataError_IsToolErrorWithSpanishMessage_AndServerKeepsWorking()
    {
        await using var client = await ConnectAsync();

        var failed = await client.CallToolAsync(
            "buscar_huecos",
            new Dictionary<string, object?> { ["serie"] = "no-existe" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(failed.IsError);
        Assert.Contains("La serie 'no-existe' no existe en el fichero de series.", Text(failed));

        var created = await client.CallToolAsync(
            "crear_pieza",
            new Dictionary<string, object?> { ["serie"] = "casos-reales", ["titulo"] = "Desde el asistente", ["fecha_libre"] = true },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(true, created.IsError);
        using var document = JsonDocument.Parse(Text(created));
        Assert.Equal("casos/03-desde-el-asistente.md", document.RootElement.GetProperty("fichero").GetString());
        Assert.True(File.Exists(Path.Combine(content.Root, "casos", "03-desde-el-asistente.md")));
    }

    // ---------- CA-5: arranque fallido ----------

    [Fact]
    public async Task Start_WithoutContentFolder_FailsWithMessage()
    {
        var run = await RunRawAsync([ServerDll], stdin: null);

        Assert.Equal(1, run.ExitCode);
        Assert.StartsWith("error: Indica la carpeta de contenido", run.Error);
        Assert.Equal(string.Empty, run.Output);
    }

    [Fact]
    public async Task Start_WithMissingContentFolder_FailsWithMessage()
    {
        var missing = Path.Combine(content.Root, "no-existe");

        var run = await RunRawAsync([ServerDll, "--contenido", missing], stdin: null);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains($"No existe la carpeta de contenido: {missing}", run.Error);
    }

    // ---------- CA-7: stdout solo lleva protocolo ----------

    [Fact]
    public async Task Stdout_CarriesOnlyJsonRpcMessages()
    {
        const string initialize = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}""";
        const string initialized = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";
        const string list = """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""";

        var run = await RunRawAsync([ServerDll, "--contenido", content.Root], stdin: [initialize, initialized, list]);

        var lines = run.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(2, lines.Length);
        foreach (var line in lines)
        {
            using var message = JsonDocument.Parse(line);
            Assert.Equal("2.0", message.RootElement.GetProperty("jsonrpc").GetString());
        }

        using var tools = JsonDocument.Parse(lines[1]);
        Assert.Equal(5, tools.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength());
    }

    private sealed record RawRun(int ExitCode, string Output, string Error);

    /// <summary>
    /// Runs the server as a plain process; with <paramref name="stdin"/>, writes those lines, waits for the replies and closes stdin.
    /// </summary>
    private static async Task<RawRun> RunRawAsync(string[] arguments, string[]? stdin)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();

        if (stdin is not null)
        {
            foreach (var line in stdin)
            {
                await process.StandardInput.WriteLineAsync(line);
                await process.StandardInput.FlushAsync();
            }

            // Give the server time to answer before closing its input, which makes it stop.
            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return new RawRun(process.ExitCode, await output, await error);
    }
}
