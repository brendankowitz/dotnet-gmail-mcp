using System.ComponentModel;
using System.Text.Json;
using GmailMcp.Services;
using ModelContextProtocol.Server;

namespace GmailMcp.Mcp.Tools;

/// <summary>
/// MCP tool for searching Gmail messages using Gmail query syntax.
/// </summary>
[McpServerToolType]
public static class SearchMessagesTool
{
    /// <summary>
    /// Searches Gmail messages using Gmail query syntax.
    /// </summary>
    /// <param name="query">Gmail search query (e.g., "from:sender@example.com", "subject:meeting", "is:unread").</param>
    /// <param name="gmailService">Gmail service injected via DI.</param>
    /// <param name="maxResults">Maximum number of results to return (default: 10, max: 100).</param>
    /// <returns>JSON array of messages with id, subject, from, to, and date.</returns>
    [McpServerTool, Description("Search Gmail messages using Gmail query syntax. Returns message metadata including id, subject, sender, recipients, and date.")]
    public static async Task<string> SearchMessages(
        [Description("Gmail search query using Gmail syntax (e.g., 'from:user@example.com', 'subject:meeting', 'is:unread')")] string query,
        IGmailService gmailService,
        [Description("Maximum number of results to return (default: 10)")] int maxResults = 10)
    {
        try
        {
            // Validate parameters
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Query parameter cannot be empty.", nameof(query));
            }

            if (maxResults < 1 || maxResults > 100)
            {
                maxResults = Math.Clamp(maxResults, 1, 100);
            }

            // Search messages
            var messages = await gmailService.SearchMessagesAsync(query, maxResults);

            // Transform to simplified format for MCP response
            var results = messages.Select(m => new
            {
                id = m.Id,
                threadId = m.ThreadId,
                subject = m.Subject,
                from = m.From,
                to = m.To,
                date = m.Date.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                hasAttachments = m.Attachments.Length > 0,
                attachmentCount = m.Attachments.Length
            }).ToArray();

            return JsonSerializer.Serialize(results, new JsonSerializerOptions
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
}
