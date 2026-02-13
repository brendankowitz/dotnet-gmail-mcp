using System.CommandLine;
using GmailMcp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GmailMcp.Commands;

/// <summary>
/// Command for authenticating with Gmail using OAuth2.
/// </summary>
public class AuthCommand : Command
{
    private AuthCommand() : base("auth", "Authenticate with Gmail using OAuth2")
    {
    }

    /// <summary>
    /// Creates and configures the auth command with dependency injection.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency resolution.</param>
    /// <returns>Configured auth command.</returns>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new AuthCommand();
        command.SetHandler(async () =>
        {
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            await ExecuteAsync(authService);
        });
        return command;
    }

    private static async Task ExecuteAsync(IAuthenticationService authService)
    {
        try
        {
            Console.WriteLine("Starting Gmail authentication flow...");
            await authService.RunAuthenticationFlowAsync();
            Console.WriteLine("✓ Authentication successful!");
            Console.WriteLine("Credentials have been saved and you can now use Gmail commands.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Authentication failed: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}
