using Ali.Core;
using Ali.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

// MCP server over stdio. stdout carries only the protocol: every log line goes to stderr.
var startup = StartupArguments.Parse(args, Environment.GetEnvironmentVariable, Environment.CurrentDirectory);
if (startup.Error is { } error)
{
    Console.Error.WriteLine($"error: {error}");
    return 1;
}

// Our own arguments are not host configuration, so they are not passed to the builder.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [] });
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton(startup.Settings!);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services
    .AddMcpServer(options => options.ServerInfo = new Implementation { Name = "applinkedin", Version = AppInfo.Version })
    .WithStdioServerTransport()
    .WithTools<AliTools>();

await builder.Build().RunAsync();
return 0;
