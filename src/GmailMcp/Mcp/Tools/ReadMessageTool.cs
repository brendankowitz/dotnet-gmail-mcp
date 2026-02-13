using System.ComponentModel;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Html.Parser;
using GmailMcp.Services;
using ModelContextProtocol.Server;

namespace GmailMcp.Mcp.Tools;

/// <summary>
/// MCP tool for reading full Gmail message content including body and attachments.
/// </summary>
[McpServerToolType]
public static class ReadMessageTool
{
    /// <summary>
    /// Reads a Gmail message by ID with full content and attachments.
    /// </summary>
    /// <param name="messageId">Unique identifier of the message to read.</param>
    /// <param name="format">Output format: 'text' for plain text only, 'html' for HTML only, 'both' for original (default).</param>
    /// <param name="gmailService">Gmail service injected via DI.</param>
    /// <returns>Formatted message with headers, body, and attachment list.</returns>
    [McpServerTool, Description("Read a Gmail message by ID with full content, headers, body, and attachment information.")]
    public static async Task<string> ReadMessage(
        [Description("Message ID to retrieve (obtained from search_messages)")] string messageId,
        [Description("Format: 'text' (plain text only), 'html' (HTML only), 'both' (original, default)")] string format = "both",
        IGmailService gmailService = null!)
    {
        try
        {
            // Validate parameters
            if (string.IsNullOrWhiteSpace(messageId))
            {
                throw new ArgumentException("Message ID cannot be empty.", nameof(messageId));
            }

            // Retrieve message
            var message = await gmailService.GetMessageAsync(messageId);

            // Format body based on requested format
            var normalizedFormat = format.ToLowerInvariant();
            var formattedBody = await GetFormattedBodyAsync(message.Body, message.IsHtml, normalizedFormat);

            // Format response with complete message details
            var result = new
            {
                id = message.Id,
                threadId = message.ThreadId,
                subject = message.Subject,
                from = message.From,
                to = message.To,
                date = message.Date.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                body = formattedBody,
                isHtml = normalizedFormat == "html" ? message.IsHtml : (normalizedFormat == "text" ? false : message.IsHtml),
                format = normalizedFormat,
                attachments = message.Attachments.Select(a => new
                {
                    id = a.Id,
                    filename = a.Filename,
                    mimeType = a.MimeType,
                    size = a.Size,
                    sizeFormatted = FormatFileSize(a.Size)
                }).ToArray()
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
                error = ex.Message,
                type = ex.GetType().Name
            });
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
