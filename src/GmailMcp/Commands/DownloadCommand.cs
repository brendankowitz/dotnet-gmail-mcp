using System.CommandLine;
using GmailMcp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GmailMcp.Commands;

/// <summary>
/// Command for downloading Gmail message attachments.
/// </summary>
public class DownloadCommand : Command
{
    private DownloadCommand() : base("download", "Download an attachment from a Gmail message")
    {
        var messageIdArgument = new Argument<string>(
            name: "message-id",
            description: "Unique identifier of the message containing the attachment");

        var attachmentIdArgument = new Argument<string>(
            name: "attachment-id",
            description: "Unique identifier of the attachment to download");

        var outputOption = new Option<string?>(
            name: "--output",
            description: "Directory to save the file (default: current directory)",
            getDefaultValue: () => null);
        outputOption.AddAlias("-o");

        var filenameOption = new Option<string?>(
            name: "--filename",
            description: "Custom filename for the downloaded file",
            getDefaultValue: () => null);
        filenameOption.AddAlias("-f");

        AddArgument(messageIdArgument);
        AddArgument(attachmentIdArgument);
        AddOption(outputOption);
        AddOption(filenameOption);
    }

    /// <summary>
    /// Creates and configures the download command with dependency injection.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency resolution.</param>
    /// <returns>Configured download command.</returns>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new DownloadCommand();
        command.SetHandler(async (messageId, attachmentId, output, filename) =>
        {
            var gmailService = serviceProvider.GetRequiredService<IGmailService>();
            await ExecuteAsync(messageId, attachmentId, output, filename, gmailService);
        },
        command.Arguments[0] as Argument<string> ?? throw new InvalidOperationException("MessageId argument not found"),
        command.Arguments[1] as Argument<string> ?? throw new InvalidOperationException("AttachmentId argument not found"),
        command.Options[0] as Option<string?> ?? throw new InvalidOperationException("Output option not found"),
        command.Options[1] as Option<string?> ?? throw new InvalidOperationException("Filename option not found"));

        return command;
    }

    private static async Task ExecuteAsync(
        string messageId,
        string attachmentId,
        string? output,
        string? filename,
        IGmailService gmailService)
    {
        try
        {
            var savePath = output ?? Directory.GetCurrentDirectory();

            // Ensure the output directory exists
            if (!Directory.Exists(savePath))
            {
                Console.WriteLine($"Creating directory: {savePath}");
                Directory.CreateDirectory(savePath);
            }

            Console.WriteLine($"Downloading attachment...");
            var filePath = await gmailService.DownloadAttachmentAsync(
                messageId,
                attachmentId,
                savePath,
                filename);

            var fileInfo = new FileInfo(filePath);
            var sizeKb = fileInfo.Length / 1024.0;

            Console.WriteLine($"✓ Download complete!");
            Console.WriteLine($"  File: {filePath}");
            Console.WriteLine($"  Size: {sizeKb:F2} KB");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Download failed: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}
