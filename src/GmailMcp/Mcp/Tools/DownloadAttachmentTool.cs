using System.ComponentModel;
using System.Text.Json;
using GmailMcp.Services;
using ModelContextProtocol.Server;

namespace GmailMcp.Mcp.Tools;

/// <summary>
/// MCP tool for downloading Gmail message attachments to the local filesystem.
/// </summary>
[McpServerToolType]
public static class DownloadAttachmentTool
{
    /// <summary>
    /// Downloads an email attachment to the local filesystem.
    /// </summary>
    /// <param name="messageId">Unique identifier of the message containing the attachment.</param>
    /// <param name="attachmentId">Unique identifier of the attachment to download.</param>
    /// <param name="gmailService">Gmail service injected via DI.</param>
    /// <param name="savePath">Directory path where the attachment should be saved (optional, defaults to current directory).</param>
    /// <param name="filename">Custom filename for the downloaded attachment (optional, uses original filename if not provided).</param>
    /// <returns>Success message with file path and size.</returns>
    [McpServerTool, Description("Download a Gmail message attachment to the local filesystem. Returns the full path and size of the downloaded file.")]
    public static async Task<string> DownloadAttachment(
        [Description("Message ID containing the attachment")] string messageId,
        [Description("Attachment ID to download (obtained from read_message)")] string attachmentId,
        IGmailService gmailService,
        [Description("Directory path to save the attachment (optional, defaults to current directory)")] string? savePath = null,
        [Description("Custom filename for the attachment (optional, uses original name if not provided)")] string? filename = null)
    {
        try
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(messageId))
            {
                throw new ArgumentException("Message ID cannot be empty.", nameof(messageId));
            }

            if (string.IsNullOrWhiteSpace(attachmentId))
            {
                throw new ArgumentException("Attachment ID cannot be empty.", nameof(attachmentId));
            }

            // Use current directory if savePath not provided
            var targetPath = string.IsNullOrWhiteSpace(savePath)
                ? Environment.CurrentDirectory
                : savePath;

            // Ensure the directory exists
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            // Download attachment
            var filePath = await gmailService.DownloadAttachmentAsync(
                messageId,
                attachmentId,
                targetPath,
                filename);

            // Get file info for response
            var fileInfo = new FileInfo(filePath);

            var result = new
            {
                success = true,
                filePath = filePath,
                filename = fileInfo.Name,
                size = fileInfo.Length,
                sizeFormatted = FormatFileSize(fileInfo.Length),
                directory = fileInfo.DirectoryName
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                type = ex.GetType().Name
            });
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:N2} {suffixes[suffixIndex]}";
    }
}
