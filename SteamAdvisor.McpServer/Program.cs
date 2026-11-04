using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SteamAdvisor.McpServer.Resources;
using SteamAdvisor.McpServer.Tools;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();

builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "SteamAdvisor.McpServer",
        Version = "1.0.0"
    };
})
.WithStdioServerTransport()
.WithResources<UserHardwareResource>()
.WithTools<SteamTools>();

var app = builder.Build();
await app.RunAsync();
