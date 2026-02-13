using System.CommandLine;
using System.Text.Json;
using GmailMcp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GmailMcp.Commands;

/// <summary>
/// Command for searching Gmail messages using Gmail query syntax.
/// </summary>
public class SearchCommand : Command
{
    private SearchCommand() : base("search", "Search Gmail messages using Gmail query syntax")
    {
        var queryArgument = new Argument<string>(
            name: "query",
            description: "Search query using Gmail syntax (e.g., 'from:user@example.com subject:invoice')");

        var maxResultsOption = new Option<int>(
            name: "--max-results",
            description: "Maximum number of results to return",
            getDefaultValue: () => 10);
        maxResultsOption.AddAlias("-n");

        var robotOption = new Option<bool>(
            name: "--robot",
            description: "Output as JSON for scripting",
            getDefaultValue: () => false);
        robotOption.AddAlias("-r");

        AddArgument(queryArgument);
        AddOption(maxResultsOption);
        AddOption(robotOption);
    }

    /// <summary>
    /// Creates and configures the search command with dependency injection.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency resolution.</param>
    /// <returns>Configured search command.</returns>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new SearchCommand();
        command.SetHandler(async (query, maxResults, robot) =>
        {
            var gmailService = serviceProvider.GetRequiredService<IGmailService>();
            await ExecuteAsync(query, maxResults, robot, gmailService);
        },
        command.Arguments[0] as Argument<string> ?? throw new InvalidOperationException("Query argument not found"),
        command.Options[0] as Option<int> ?? throw new InvalidOperationException("MaxResults option not found"),
        command.Options[1] as Option<bool> ?? throw new InvalidOperationException("Robot option not found"));

        return command;
    }

    private static async Task ExecuteAsync(string query, int maxResults, bool robot, IGmailService gmailService)
    {
        try
        {
            var messages = await gmailService.SearchMessagesAsync(query, maxResults);

            if (robot)
            {
                var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                Console.WriteLine(json);
            }
            else
            {
                if (messages.Count == 0)
                {
                    Console.WriteLine("No messages found matching the query.");
                    return;
                }

                Console.WriteLine($"Found {messages.Count} message(s):\n");
                for (int i = 0; i < messages.Count; i++)
                {
                    var msg = messages[i];
                    Console.WriteLine($"{i + 1}. {msg.Subject}");
                    Console.WriteLine($"   From: {msg.From}");
                    Console.WriteLine($"   Date: {msg.Date:yyyy-MM-dd HH:mm}");
                    Console.WriteLine($"   ID: {msg.Id}");
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Search failed: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}
