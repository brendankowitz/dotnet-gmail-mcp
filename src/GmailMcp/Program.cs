using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using GmailMcp.Commands;
using GmailMcp.Services;

namespace GmailMcp;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Create and configure dependency injection container
        var services = new ServiceCollection();
        ConfigureServices(services);

        await using var serviceProvider = services.BuildServiceProvider();

        // Ensure config directory exists before running any commands
        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
        await authService.EnsureConfigDirectoryAsync();

        // Create root command with all subcommands
        var rootCommand = new RootCommand("Gmail MCP Server - Dual-mode CLI and MCP integration for Gmail")
        {
            AuthCommand.Create(serviceProvider),
            SearchCommand.Create(serviceProvider),
            ReadCommand.Create(serviceProvider),
            DownloadCommand.Create(serviceProvider),
            McpCommand.Create(serviceProvider)
        };

        return await rootCommand.InvokeAsync(args);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register services as singletons
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IGmailService, GmailService>();
    }
}
