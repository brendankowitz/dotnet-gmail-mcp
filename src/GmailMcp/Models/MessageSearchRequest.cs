namespace GmailMcp.Models;

/// <summary>
/// Represents parameters for searching Gmail messages.
/// </summary>
public record MessageSearchRequest
{
    /// <summary>
    /// Gmail search query using Gmail's search syntax.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Maximum number of results to return. Defaults to 10.
    /// </summary>
    public int MaxResults { get; init; } = 10;
}
