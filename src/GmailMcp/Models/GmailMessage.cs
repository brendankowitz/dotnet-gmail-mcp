namespace GmailMcp.Models;

/// <summary>
/// Represents a Gmail message with its metadata and content.
/// </summary>
public record GmailMessage
{
    /// <summary>
    /// Unique identifier for the message.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Thread identifier that this message belongs to.
    /// </summary>
    public required string ThreadId { get; init; }

    /// <summary>
    /// Subject line of the message.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Sender's email address.
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// Array of recipient email addresses.
    /// </summary>
    public required string[] To { get; init; }

    /// <summary>
    /// Date and time when the message was sent.
    /// </summary>
    public required DateTimeOffset Date { get; init; }

    /// <summary>
    /// Body content of the message.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Indicates whether the body content is HTML formatted.
    /// </summary>
    public required bool IsHtml { get; init; }

    /// <summary>
    /// Array of attachments included with the message.
    /// </summary>
    public required GmailAttachment[] Attachments { get; init; }
}
