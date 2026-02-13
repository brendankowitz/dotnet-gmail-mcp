namespace GmailMcp.Models;

/// <summary>
/// Represents metadata for a Gmail message attachment.
/// </summary>
/// <param name="Id">Unique identifier for the attachment.</param>
/// <param name="Filename">Name of the attachment file.</param>
/// <param name="MimeType">MIME type of the attachment (e.g., "application/pdf").</param>
/// <param name="Size">Size of the attachment in bytes.</param>
public record GmailAttachment(
    string Id,
    string Filename,
    string MimeType,
    long Size
);
