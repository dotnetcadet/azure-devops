using Azure.DevOps.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdout is the MCP JSON-RPC channel — all logging must go to stderr or the protocol breaks.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// The connection context (org/project/repo/PAT) is resolved once and shared by every tool.
builder.Services.AddSingleton<AzureDevOpsContext>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
