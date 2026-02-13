using System.CommandLine;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Html.Parser;
using GmailMcp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GmailMcp.Commands;

/// <summary>
/// Command for reading a specific Gmail message by ID.
/// </summary>
public class ReadCommand : Command
{
    private ReadCommand() : base("read", "Read a specific Gmail message by ID")
    {
        var messageIdArgument = new Argument<string>(
            name: "message-id",
            description: "Unique identifier of the message to read");

        var robotOption = new Option<bool>(
            name: "--robot",
            description: "Output as JSON for scripting",
            getDefaultValue: () => false);
        robotOption.AddAlias("-r");

        var formatOption = new Option<string>(
            name: "--format",
            description: "Output format: text (plain text only), html (HTML only), or both (default)",
            getDefaultValue: () => "both");
        formatOption.AddAlias("-f");

        AddArgument(messageIdArgument);
        AddOption(robotOption);
        AddOption(formatOption);
    }

    /// <summary>
    /// Creates and configures the read command with dependency injection.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency resolution.</param>
    /// <returns>Configured read command.</returns>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new ReadCommand();
        command.SetHandler(async (messageId, robot, format) =>
        {
            var gmailService = serviceProvider.GetRequiredService<IGmailService>();
            await ExecuteAsync(messageId, robot, format, gmailService);
        },
        command.Arguments[0] as Argument<string> ?? throw new InvalidOperationException("MessageId argument not found"),
        command.Options[0] as Option<bool> ?? throw new InvalidOperationException("Robot option not found"),
        command.Options[1] as Option<string> ?? throw new InvalidOperationException("Format option not found"));

        return command;
    }

    private static async Task ExecuteAsync(string messageId, bool robot, string format, IGmailService gmailService)
    {
        try
        {
            var message = await gmailService.GetMessageAsync(messageId);

            // Validate format option
            var normalizedFormat = format.ToLowerInvariant();
            if (normalizedFormat is not ("text" or "html" or "both"))
            {
                Console.Error.WriteLine($"Invalid format '{format}'. Valid options: text, html, both");
                Environment.ExitCode = 1;
                return;
            }

            if (robot)
            {
                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                Console.WriteLine(json);
            }
            else
            {
                Console.WriteLine($"Subject: {message.Subject}");
                Console.WriteLine($"From: {message.From}");
                Console.WriteLine($"To: {string.Join(", ", message.To)}");
                Console.WriteLine($"Date: {message.Date:yyyy-MM-dd HH:mm:ss zzz}");
                Console.WriteLine($"Thread ID: {message.ThreadId}");
                Console.WriteLine($"Message ID: {message.Id}");
                Console.WriteLine();
                Console.WriteLine("--- Message Body ---");

                // Display body based on format option
                var bodyToDisplay = await GetFormattedBodyAsync(message.Body, message.IsHtml, normalizedFormat);
                Console.WriteLine(bodyToDisplay);
                Console.WriteLine();

                if (message.Attachments.Length > 0)
                {
                    Console.WriteLine($"Attachments ({message.Attachments.Length}):");
                    foreach (var attachment in message.Attachments)
                    {
                        var sizeKb = attachment.Size / 1024.0;
                        Console.WriteLine($"  - {attachment.Filename} ({sizeKb:F2} KB, {attachment.MimeType})");
                        Console.WriteLine($"    ID: {attachment.Id}");
                    }
                }
                else
                {
                    Console.WriteLine("No attachments.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read message: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Formats the message body based on the requested format.
    /// </summary>
    /// <param name="body">The message body content.</param>
    /// <param name="isHtml">Whether the body is HTML.</param>
    /// <param name="format">The requested format: text, html, or both.</param>
    /// <returns>Formatted body content.</returns>
    private static async Task<string> GetFormattedBodyAsync(string body, bool isHtml, string format)
    {
        return format switch
        {
            "text" => isHtml ? await ConvertHtmlToTextAsync(body) : body,
            "html" => isHtml ? body : body, // Already plain text, just return it
            "both" => body, // Return as-is (current behavior)
            _ => body
        };
    }

    /// <summary>
    /// Converts HTML content to plain text using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content to convert.</param>
    /// <returns>Plain text representation.</returns>
    private static async Task<string> ConvertHtmlToTextAsync(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        try
        {
            var context = BrowsingContext.New(Configuration.Default);
            var parser = context.GetService<IHtmlParser>();

            if (parser == null)
                return html; // Fallback to original HTML if parser unavailable

            var document = await parser.ParseDocumentAsync(html);
            return document.Body?.TextContent ?? html;
        }
        catch
        {
            // If parsing fails, return original HTML
            return html;
        }
    }
}
