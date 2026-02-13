using GmailMcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace GmailMcp.Mcp;

/// <summary>
/// Main MCP server implementation for Gmail integration.
/// Provides stdio-based Model Context Protocol server for AI assistant access to Gmail.
/// </summary>
public static class GmailMcpServer
{
    /// <summary>
    /// Runs the MCP server using stdio transport for AI assistant integration.
    /// </summary>
    /// <param name="gmailService">Gmail service for message operations.</param>
    /// <param name="authService">Authentication service for credential management.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    public static async Task RunAsync(
        IGmailService gmailService,
        IAuthenticationService authService,
        CancellationToken ct = default)
    {
        var builder = Host.CreateApplicationBuilder();

        // Configure logging to stderr to avoid interfering with stdio protocol
        // In MCP mode, stdout is reserved for JSON-RPC, so ALL logs must go to stderr
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
        });
        builder.Logging.AddConsole(options =>
        {
            // Send ALL log levels to stderr to avoid polluting stdout
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Register services for dependency injection
        builder.Services.AddSingleton(gmailService);
        builder.Services.AddSingleton(authService);

        // Configure MCP server with stdio transport
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        var host = builder.Build();
        await host.RunAsync(ct);
    }
}
