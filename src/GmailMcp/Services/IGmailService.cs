using GmailMcp.Models;

namespace GmailMcp.Services;

/// <summary>
/// Defines operations for interacting with Gmail messages and attachments.
/// </summary>
public interface IGmailService : IDisposable
{
    /// <summary>
    /// Searches for Gmail messages matching the specified query.
    /// </summary>
    /// <param name="query">Gmail search query using Gmail's search syntax.</param>
    /// <param name="maxResults">Maximum number of results to return. Defaults to 10.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of messages matching the search criteria.</returns>
    Task<IReadOnlyList<GmailMessage>> SearchMessagesAsync(
        string query,
        int maxResults = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a specific Gmail message by its ID.
    /// </summary>
    /// <param name="messageId">Unique identifier of the message to retrieve.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The requested Gmail message.</returns>
    Task<GmailMessage> GetMessageAsync(
        string messageId,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads an attachment from a Gmail message to the specified path.
    /// </summary>
    /// <param name="messageId">Unique identifier of the message containing the attachment.</param>
    /// <param name="attachmentId">Unique identifier of the attachment to download.</param>
    /// <param name="savePath">Directory path where the attachment should be saved.</param>
    /// <param name="filename">Optional custom filename. If null, uses the original attachment filename.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The full path to the downloaded attachment file.</returns>
    Task<string> DownloadAttachmentAsync(
        string messageId,
        string attachmentId,
        string savePath,
        string? filename = null,
        CancellationToken ct = default);
}
