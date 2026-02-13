using Google.Apis.Auth.OAuth2;

namespace GmailMcp.Services;

/// <summary>
/// Defines operations for managing Gmail API authentication and credentials.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Ensures that the configuration directory for storing credentials exists.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    Task EnsureConfigDirectoryAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks whether valid user credentials are currently available.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>True if valid credentials exist; otherwise, false.</returns>
    Task<bool> HasValidCredentialsAsync(CancellationToken ct = default);

    /// <summary>
    /// Executes the OAuth2 authentication flow to obtain user credentials.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    Task RunAuthenticationFlowAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current user credentials, initiating authentication if necessary.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The user credentials for accessing the Gmail API.</returns>
    Task<UserCredential> GetCredentialsAsync(CancellationToken ct = default);
}
