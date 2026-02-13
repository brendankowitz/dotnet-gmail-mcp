using System.CommandLine;
using GmailMcp.Mcp;
using GmailMcp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GmailMcp.Commands;

/// <summary>
/// Command for starting the MCP server for AI assistant integration.
/// </summary>
public class McpCommand : Command
{
    private McpCommand() : base("mcp", "Start MCP server for AI assistant integration via stdio")
    {
    }

    /// <summary>
    /// Creates and configures the MCP command with dependency injection.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency resolution.</param>
    /// <returns>Configured MCP command.</returns>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new McpCommand();
        command.SetHandler(async () =>
        {
            var gmailService = serviceProvider.GetRequiredService<IGmailService>();
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            await ExecuteAsync(gmailService, authService);
        });
        return command;
    }

    private static async Task ExecuteAsync(
        IGmailService gmailService,
        IAuthenticationService authService)
    {
        try
        {
            // Ensure credentials exist before starting MCP server
            if (!await authService.HasValidCredentialsAsync())
            {
                // Write error to stderr (not stdout, to avoid interfering with MCP protocol)
                await Console.Error.WriteLineAsync("Error: No valid credentials found. Please run 'dotnet-gmail auth' first.");
                Environment.ExitCode = 1;
                return;
            }

            // Start MCP server - no console output, only stdio protocol
            await GmailMcpServer.RunAsync(gmailService, authService);
        }
        catch (Exception ex)
        {
            // Write error to stderr (not stdout, to avoid interfering with MCP protocol)
            await Console.Error.WriteLineAsync($"MCP server error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}
