using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using GmailMcp.Models;
using System.Text;
using Polly;
using Polly.Retry;

namespace GmailMcp.Services;

/// <summary>
/// Gmail API wrapper service for managing messages and attachments.
/// </summary>
public sealed class GmailService : IGmailService, IDisposable
{
    private readonly IAuthenticationService _authService;
    private Google.Apis.Gmail.v1.GmailService? _gmailService;
    private bool _disposed;

    /// <summary>
    /// Retry policy for Gmail API calls with exponential backoff.
    /// </summary>
    private static readonly ResiliencePipeline _retryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(1),
            ShouldHandle = new PredicateBuilder().Handle<Google.GoogleApiException>(ex =>
                ex.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                ex.HttpStatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                ex.HttpStatusCode == System.Net.HttpStatusCode.InternalServerError)
        })
        .Build();

    /// <summary>
    /// Initializes a new instance of the GmailService.
    /// </summary>
    /// <param name="authService">Authentication service for obtaining credentials.</param>
    public GmailService(IAuthenticationService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    /// <summary>
    /// Gets or initializes the Gmail service instance with authenticated credentials.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>Initialized Gmail service instance.</returns>
    /// <remarks>
    /// This method uses lazy initialization to create the Gmail service only when needed.
    /// The service is cached for subsequent calls.
    /// </remarks>
    private async Task<Google.Apis.Gmail.v1.GmailService> GetGmailServiceAsync(CancellationToken ct)
    {
        if (_gmailService is not null)
        {
            return _gmailService;
        }

        var credentials = await _authService.GetCredentialsAsync(ct);
        _gmailService = new Google.Apis.Gmail.v1.GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credentials,
            ApplicationName = "Gmail MCP Server"
        });

        return _gmailService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GmailMessage>> SearchMessagesAsync(
        string query,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        try
        {
            var gmail = await GetGmailServiceAsync(ct);

            var listRequest = gmail.Users.Messages.List("me");
            listRequest.Q = query;
            listRequest.MaxResults = maxResults;

            var listResponse = await _retryPipeline.ExecuteAsync(
                async ct => await listRequest.ExecuteAsync(ct), ct);
            var messages = listResponse.Messages ?? [];

            if (messages.Count == 0)
            {
                return Array.Empty<GmailMessage>();
            }

            // Fetch message details for each result
            var messageTasks = messages.Select(async msg =>
            {
                var getRequest = gmail.Users.Messages.Get("me", msg.Id);
                getRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
                getRequest.MetadataHeaders = new Google.Apis.Util.Repeatable<string>(new[] { "Subject", "From", "To", "Date" });

                var detail = await _retryPipeline.ExecuteAsync(
                    async ct => await getRequest.ExecuteAsync(ct), ct);
                var headers = detail.Payload?.Headers ?? [];

                return new GmailMessage
                {
                    Id = detail.Id,
                    ThreadId = detail.ThreadId,
                    Subject = GetHeaderValue(headers, "Subject"),
                    From = GetHeaderValue(headers, "From"),
                    To = GetHeaderValue(headers, "To").Split(',', StringSplitOptions.TrimEntries),
                    Date = ParseDate(GetHeaderValue(headers, "Date")),
                    Body = string.Empty,
                    IsHtml = false,
                    Attachments = []
                };
            });

            var results = await Task.WhenAll(messageTasks);
            return results;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to search Gmail messages: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<GmailMessage> GetMessageAsync(
        string messageId,
        CancellationToken ct = default)
    {
        try
        {
            var gmail = await GetGmailServiceAsync(ct);

            var getRequest = gmail.Users.Messages.Get("me", messageId);
            getRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

            var message = await _retryPipeline.ExecuteAsync(
                async ct => await getRequest.ExecuteAsync(ct), ct);
            var headers = message.Payload?.Headers ?? [];

            // Parse headers
            var subject = GetHeaderValue(headers, "Subject");
            var from = GetHeaderValue(headers, "From");
            var to = GetHeaderValue(headers, "To");
            var date = GetHeaderValue(headers, "Date");

            // Extract email body
            var (body, isHtml) = ExtractEmailContent(message.Payload);

            // Extract attachments
            var attachments = ExtractAttachments(message.Payload);

            return new GmailMessage
            {
                Id = message.Id,
                ThreadId = message.ThreadId,
                Subject = subject,
                From = from,
                To = to.Split(',', StringSplitOptions.TrimEntries),
                Date = ParseDate(date),
                Body = body,
                IsHtml = isHtml,
                Attachments = attachments
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve Gmail message: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<string> DownloadAttachmentAsync(
        string messageId,
        string attachmentId,
        string savePath,
        string? filename = null,
        CancellationToken ct = default)
    {
        try
        {
            // Validate savePath
            if (string.IsNullOrWhiteSpace(savePath))
            {
                throw new ArgumentException("Save path cannot be null or empty", nameof(savePath));
            }

            if (!Path.IsPathRooted(savePath))
            {
                throw new ArgumentException("Save path must be an absolute path", nameof(savePath));
            }

            if (savePath.Contains(".."))
            {
                throw new ArgumentException("Save path cannot contain '..' path traversal", nameof(savePath));
            }

            var gmail = await GetGmailServiceAsync(ct);

            // Get attachment data
            var attachmentRequest = gmail.Users.Messages.Attachments.Get("me", messageId, attachmentId);
            var attachment = await _retryPipeline.ExecuteAsync(
                async ct => await attachmentRequest.ExecuteAsync(ct), ct);

            if (string.IsNullOrEmpty(attachment.Data))
            {
                throw new InvalidOperationException("No attachment data received");
            }

            // Decode base64url data to bytes
            var data = DecodeBase64Url(attachment.Data);

            // Get original filename if not provided
            if (string.IsNullOrEmpty(filename))
            {
                filename = await GetAttachmentFilenameAsync(gmail, messageId, attachmentId, ct);
            }

            // Validate filename
            if (!string.IsNullOrEmpty(filename))
            {
                var invalidChars = Path.GetInvalidFileNameChars();
                if (filename.Any(c => invalidChars.Contains(c)))
                {
                    throw new ArgumentException("Filename contains invalid characters", nameof(filename));
                }

                if (filename.Contains(".."))
                {
                    throw new ArgumentException("Filename cannot contain '..' path traversal", nameof(filename));
                }
            }

            // Check cancellation before directory operations
            ct.ThrowIfCancellationRequested();

            // Create save directory if it doesn't exist
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            // Use Path.GetFullPath to resolve and validate final path
            var fullPath = Path.GetFullPath(Path.Combine(savePath, filename));
            var normalizedSavePath = Path.GetFullPath(savePath);

            // Ensure the final path is within the save directory
            if (!fullPath.StartsWith(normalizedSavePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid file path: resolved path is outside the save directory", nameof(filename));
            }

            // Write bytes to file
            await File.WriteAllBytesAsync(fullPath, data, ct);

            return fullPath;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException($"Failed to download attachment: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts email body content from MIME message parts recursively.
    /// </summary>
    /// <param name="messagePart">The message part to extract content from.</param>
    /// <returns>A tuple containing the extracted body content and a flag indicating if it's HTML.</returns>
    /// <remarks>
    /// Prefers plain text content over HTML. If both exist, plain text is returned.
    /// </remarks>
    private static (string Body, bool IsHtml) ExtractEmailContent(MessagePart? messagePart)
    {
        if (messagePart is null)
        {
            return (string.Empty, false);
        }

        var textContent = new StringBuilder();
        var htmlContent = new StringBuilder();

        ExtractContentRecursive(messagePart, textContent, htmlContent);

        // Prefer plain text, fall back to HTML
        if (textContent.Length > 0)
        {
            return (textContent.ToString(), false);
        }

        if (htmlContent.Length > 0)
        {
            return (htmlContent.ToString(), true);
        }

        return (string.Empty, false);
    }

    /// <summary>
    /// Recursively processes MIME parts to extract text and HTML content.
    /// </summary>
    /// <param name="part">The message part to process.</param>
    /// <param name="textContent">StringBuilder to accumulate plain text content.</param>
    /// <param name="htmlContent">StringBuilder to accumulate HTML content.</param>
    private static void ExtractContentRecursive(
        MessagePart part,
        StringBuilder textContent,
        StringBuilder htmlContent)
    {
        // If the part has body data, process it based on MIME type
        if (part.Body?.Data is not null)
        {
            var content = DecodeBase64Url(part.Body.Data);
            var decodedContent = Encoding.UTF8.GetString(content);

            if (part.MimeType == "text/plain")
            {
                textContent.Append(decodedContent);
            }
            else if (part.MimeType == "text/html")
            {
                htmlContent.Append(decodedContent);
            }
        }

        // If the part has nested parts, recursively process them
        if (part.Parts is not null)
        {
            foreach (var subPart in part.Parts)
            {
                ExtractContentRecursive(subPart, textContent, htmlContent);
            }
        }
    }

    /// <summary>
    /// Extracts attachment information from message parts recursively.
    /// </summary>
    /// <param name="messagePart">The message part to extract attachments from.</param>
    /// <returns>An array of Gmail attachments found in the message.</returns>
    private static GmailAttachment[] ExtractAttachments(MessagePart? messagePart)
    {
        if (messagePart is null)
        {
            return [];
        }

        var attachments = new List<GmailAttachment>();
        ExtractAttachmentsRecursive(messagePart, attachments);
        return [.. attachments];
    }

    /// <summary>
    /// Recursively processes MIME parts to find attachments.
    /// </summary>
    /// <param name="part">The message part to process.</param>
    /// <param name="attachments">List to accumulate found attachments.</param>
    private static void ExtractAttachmentsRecursive(MessagePart part, List<GmailAttachment> attachments)
    {
        // Check if this part is an attachment
        if (part.Body?.AttachmentId is not null)
        {
            var filename = part.Filename ?? $"attachment-{part.Body.AttachmentId}";
            var mimeType = part.MimeType ?? "application/octet-stream";
            var size = part.Body.Size ?? 0;

            attachments.Add(new GmailAttachment(
                Id: part.Body.AttachmentId,
                Filename: filename,
                MimeType: mimeType,
                Size: size
            ));
        }

        // If the part has nested parts, recursively process them
        if (part.Parts is not null)
        {
            foreach (var subPart in part.Parts)
            {
                ExtractAttachmentsRecursive(subPart, attachments);
            }
        }
    }

    /// <summary>
    /// Gets the original filename for an attachment.
    /// </summary>
    /// <param name="gmail">Gmail service instance.</param>
    /// <param name="messageId">Message ID containing the attachment.</param>
    /// <param name="attachmentId">Attachment ID to find the filename for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The original filename or a generated name if not found.</returns>
    private static async Task<string> GetAttachmentFilenameAsync(
        Google.Apis.Gmail.v1.GmailService gmail,
        string messageId,
        string attachmentId,
        CancellationToken ct)
    {
        var getRequest = gmail.Users.Messages.Get("me", messageId);
        getRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

        var message = await _retryPipeline.ExecuteAsync(
            async ct => await getRequest.ExecuteAsync(ct), ct);

        // Find the attachment part to get original filename
        var filename = FindAttachmentFilename(message.Payload, attachmentId);
        return filename ?? $"attachment-{attachmentId}";
    }

    /// <summary>
    /// Recursively searches for an attachment filename by ID.
    /// </summary>
    /// <param name="part">The message part to search.</param>
    /// <param name="attachmentId">The attachment ID to find.</param>
    /// <returns>The filename if found, otherwise null.</returns>
    private static string? FindAttachmentFilename(MessagePart? part, string attachmentId)
    {
        if (part is null)
        {
            return null;
        }

        if (part.Body?.AttachmentId == attachmentId)
        {
            return part.Filename;
        }

        if (part.Parts is not null)
        {
            foreach (var subPart in part.Parts)
            {
                var filename = FindAttachmentFilename(subPart, attachmentId);
                if (filename is not null)
                {
                    return filename;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Decodes base64url encoded data to bytes.
    /// </summary>
    /// <param name="base64Url">The base64url encoded string.</param>
    /// <returns>Decoded byte array.</returns>
    private static byte[] DecodeBase64Url(string base64Url)
    {
        // Convert base64url to standard base64
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');

        // Add padding if necessary
        var padLength = (4 - base64.Length % 4) % 4;
        if (padLength > 0)
        {
            base64 += new string('=', padLength);
        }

        return Convert.FromBase64String(base64);
    }

    /// <summary>
    /// Gets a header value by name (case-insensitive).
    /// </summary>
    /// <param name="headers">List of message headers.</param>
    /// <param name="name">Header name to search for.</param>
    /// <returns>The header value or an empty string if not found.</returns>
    private static string GetHeaderValue(IList<MessagePartHeader> headers, string name)
    {
        var header = headers.FirstOrDefault(h =>
            string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase));
        return header?.Value ?? string.Empty;
    }

    /// <summary>
    /// Parses a date string to DateTimeOffset.
    /// </summary>
    /// <param name="dateString">The date string to parse.</param>
    /// <returns>Parsed DateTimeOffset or current UTC time if parsing fails.</returns>
    private static DateTimeOffset ParseDate(string dateString)
    {
        if (string.IsNullOrEmpty(dateString))
        {
            return DateTimeOffset.UtcNow;
        }

        if (DateTimeOffset.TryParse(dateString, out var date))
        {
            return date;
        }

        return DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Disposes of managed resources, including the Gmail service instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _gmailService?.Dispose();
            _disposed = true;
        }
    }
}
