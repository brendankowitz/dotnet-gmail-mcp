using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace GmailMcp.Services;

/// <summary>
/// Implements OAuth2 authentication for Gmail API access.
/// </summary>
public sealed class AuthenticationService : IAuthenticationService, IDisposable
{
    private const string ClientSecretsFileName = "gcp-oauth.keys.json";
    private const string CredentialsFileName = "credentials.json";
    private const string ConfigDirectoryName = ".gmail-mcp";
    private const int CallbackPort = 3000;
    private const string CallbackPath = "/oauth2callback";

    private static readonly string[] RequiredScopes =
    [
        "https://www.googleapis.com/auth/gmail.modify"
    ];

    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly string _configDirectory;
    private readonly string _clientSecretsPath;
    private readonly string _credentialsPath;
    private HttpListener? _httpListener;

    public AuthenticationService()
    {
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ConfigDirectoryName
        );
        _clientSecretsPath = Path.Combine(_configDirectory, ClientSecretsFileName);
        _credentialsPath = Path.Combine(_configDirectory, CredentialsFileName);
    }

    /// <inheritdoc />
    public Task EnsureConfigDirectoryAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> HasValidCredentialsAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_credentialsPath))
        {
            return false;
        }

        try
        {
            var credentialsJson = await File.ReadAllTextAsync(_credentialsPath, ct);
            using var doc = JsonDocument.Parse(credentialsJson);
            var root = doc.RootElement;

            // Check if refresh token exists
            if (!root.TryGetProperty("refresh_token", out var refreshToken))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(refreshToken.GetString());
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task RunAuthenticationFlowAsync(CancellationToken ct = default)
    {
        await EnsureConfigDirectoryAsync(ct);

        // Check if client secrets file exists
        if (!File.Exists(_clientSecretsPath))
        {
            throw new InvalidOperationException(
                $"OAuth client credentials file not found. Please place '{ClientSecretsFileName}' in: {_configDirectory}\n" +
                "You can download this file from Google Cloud Console:\n" +
                "1. Go to https://console.cloud.google.com/apis/credentials\n" +
                "2. Create OAuth 2.0 Client ID (type: Desktop app or Web application)\n" +
                "3. Download the JSON file and save it as 'gcp-oauth.keys.json'"
            );
        }

        // Load client secrets
        var clientSecrets = await LoadClientSecretsAsync(ct);

        // Create redirect URI
        var redirectUri = $"http://localhost:{CallbackPort}{CallbackPath}";

        // Start HTTP listener for OAuth callback
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://localhost:{CallbackPort}/");
        _httpListener.Start();

        try
        {
            // Generate authorization URL
            var authorizationUrl = GenerateAuthorizationUrl(clientSecrets, redirectUri);

            // Open browser to authorization URL
            Console.WriteLine("Opening browser for authentication...");
            Console.WriteLine($"If the browser doesn't open automatically, please visit: {authorizationUrl}");
            OpenBrowser(authorizationUrl);

            // Wait for callback
            var authorizationCode = await WaitForAuthorizationCallbackAsync(ct);

            // Exchange authorization code for tokens
            var tokens = await ExchangeCodeForTokensAsync(
                clientSecrets,
                authorizationCode,
                redirectUri,
                ct
            );

            // Save credentials
            await SaveCredentialsAsync(tokens, ct);

            Console.WriteLine("Authentication successful!");
        }
        finally
        {
            _httpListener?.Stop();
            _httpListener?.Close();
            _httpListener = null;
        }
    }

    /// <inheritdoc />
    public async Task<UserCredential> GetCredentialsAsync(CancellationToken ct = default)
    {
        if (!await HasValidCredentialsAsync(ct))
        {
            throw new InvalidOperationException(
                "No valid credentials found. Please run authentication flow first."
            );
        }

        var clientSecrets = await LoadClientSecretsAsync(ct);

        // Create a file data store for token management
        var fileDataStore = new FileDataStore(_configDirectory, fullPath: true);

        // Create authorization code flow
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = clientSecrets,
            Scopes = RequiredScopes,
            DataStore = fileDataStore
        });

        // Load stored token
        var token = await LoadTokenResponseAsync(ct);

        // Create user credential
        var credential = new UserCredential(flow, "user", token);

        // Refresh token if needed (using IsStale instead of deprecated IsExpired)
        if (token.IsStale)
        {
            await credential.RefreshTokenAsync(ct);
        }

        return credential;
    }

    /// <summary>
    /// Loads OAuth client secrets from the configuration file.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Client secrets containing client ID and secret.</returns>
    private async Task<ClientSecrets> LoadClientSecretsAsync(CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(_clientSecretsPath, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Support both "installed" (Desktop app) and "web" credential types
        JsonElement clientData;
        if (root.TryGetProperty("installed", out var installedData))
        {
            clientData = installedData;
        }
        else if (root.TryGetProperty("web", out var webData))
        {
            clientData = webData;
        }
        else
        {
            throw new InvalidOperationException(
                "Invalid OAuth credentials file format. " +
                "File should contain either 'installed' or 'web' credentials from Google Cloud Console."
            );
        }

        var clientId = clientData.GetProperty("client_id").GetString();
        var clientSecret = clientData.GetProperty("client_secret").GetString();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Invalid OAuth credentials: client_id and client_secret are required."
            );
        }

        return new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        };
    }

    /// <summary>
    /// Generates the OAuth authorization URL for user consent.
    /// </summary>
    /// <param name="clientSecrets">Client secrets containing OAuth credentials.</param>
    /// <param name="redirectUri">Redirect URI for the OAuth callback.</param>
    /// <returns>The authorization URL to open in the browser.</returns>
    private static string GenerateAuthorizationUrl(ClientSecrets clientSecrets, string redirectUri)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = clientSecrets.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(" ", RequiredScopes),
            ["access_type"] = "offline",
            ["prompt"] = "consent"
        };

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"
        ));

        return $"https://accounts.google.com/o/oauth2/v2/auth?{queryString}";
    }

    /// <summary>
    /// Opens the default browser to the specified URL.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open browser automatically: {ex.Message}");
        }
    }

    /// <summary>
    /// Waits for the OAuth callback from the browser with cancellation support.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The authorization code from the callback.</returns>
    private async Task<string> WaitForAuthorizationCallbackAsync(CancellationToken ct)
    {
        if (_httpListener == null)
        {
            throw new InvalidOperationException("HTTP listener not initialized.");
        }

        while (!ct.IsCancellationRequested)
        {
            // Create a task for getting the context
            var contextTask = _httpListener.GetContextAsync();
            var delayTask = Task.Delay(Timeout.Infinite, ct);

            // Wait for either the context or cancellation
            var completedTask = await Task.WhenAny(contextTask, delayTask);

            // If cancelled, throw
            if (completedTask == delayTask)
            {
                ct.ThrowIfCancellationRequested();
            }

            var context = await contextTask;
            var request = context.Request;
            var response = context.Response;

            try
            {
                if (request.Url?.AbsolutePath == CallbackPath)
                {
                    var queryParams = ParseQueryString(request.Url.Query);
                    var code = queryParams.GetValueOrDefault("code");
                    var error = queryParams.GetValueOrDefault("error");

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        await SendResponseAsync(response, 400, $"Authentication failed: {error}");
                        throw new InvalidOperationException($"OAuth error: {error}");
                    }

                    if (string.IsNullOrWhiteSpace(code))
                    {
                        await SendResponseAsync(response, 400, "No authorization code received");
                        throw new InvalidOperationException("No authorization code in callback");
                    }

                    await SendResponseAsync(
                        response,
                        200,
                        "Authentication successful! You can close this window and return to the application."
                    );

                    return code;
                }
            }
            finally
            {
                response.Close();
            }
        }

        throw new OperationCanceledException("Authentication was cancelled.");
    }

    /// <summary>
    /// Parses a URL query string into a dictionary of key-value pairs.
    /// </summary>
    /// <param name="query">The query string to parse.</param>
    /// <returns>Dictionary of query parameters.</returns>
    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        // Remove leading '?' if present
        query = query.TrimStart('?');

        foreach (var pair in query.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = Uri.UnescapeDataString(parts[0]);
                var value = Uri.UnescapeDataString(parts[1]);
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Sends an HTML response to the OAuth callback request.
    /// </summary>
    /// <param name="response">The HTTP response object.</param>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="message">Message to display to the user.</param>
    private static async Task SendResponseAsync(HttpListenerResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";

        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Gmail MCP Authentication</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }}
        .container {{
            background: white;
            padding: 2rem;
            border-radius: 10px;
            box-shadow: 0 10px 25px rgba(0,0,0,0.2);
            text-align: center;
            max-width: 400px;
        }}
        .status {{
            color: {(statusCode == 200 ? "#10b981" : "#ef4444")};
            font-size: 1.5rem;
            font-weight: bold;
            margin-bottom: 1rem;
        }}
        .message {{
            color: #374151;
            font-size: 1rem;
            line-height: 1.5;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='status'>{(statusCode == 200 ? "✓" : "✗")} {(statusCode == 200 ? "Success" : "Error")}</div>
        <div class='message'>{message}</div>
    </div>
</body>
</html>";

        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Exchanges an authorization code for OAuth access and refresh tokens.
    /// </summary>
    /// <param name="clientSecrets">OAuth client secrets.</param>
    /// <param name="authorizationCode">Authorization code from the callback.</param>
    /// <param name="redirectUri">Redirect URI used in the authorization request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Token response containing access and refresh tokens.</returns>
    private async Task<TokenResponse> ExchangeCodeForTokensAsync(
        ClientSecrets clientSecrets,
        string authorizationCode,
        string redirectUri,
        CancellationToken ct)
    {
        var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = authorizationCode,
            ["client_id"] = clientSecrets.ClientId,
            ["client_secret"] = clientSecrets.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await _httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            requestContent,
            ct
        );

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        return new TokenResponse
        {
            AccessToken = root.GetProperty("access_token").GetString(),
            RefreshToken = root.TryGetProperty("refresh_token", out var refreshToken)
                ? refreshToken.GetString()
                : null,
            ExpiresInSeconds = root.TryGetProperty("expires_in", out var expiresIn)
                ? expiresIn.GetInt64()
                : null,
            TokenType = root.TryGetProperty("token_type", out var tokenType)
                ? tokenType.GetString()
                : "Bearer",
            Scope = root.TryGetProperty("scope", out var scope)
                ? scope.GetString()
                : null,
            IssuedUtc = DateTime.UtcNow
        };
    }

    private const int DefaultTokenExpirySeconds = 3600; // 1 hour

    /// <summary>
    /// Saves OAuth credentials to disk with restrictive file permissions.
    /// </summary>
    /// <param name="token">Token response to save.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task SaveCredentialsAsync(TokenResponse token, CancellationToken ct)
    {
        var credentialsData = new
        {
            access_token = token.AccessToken,
            refresh_token = token.RefreshToken,
            scope = token.Scope,
            token_type = token.TokenType,
            expiry_date = token.IssuedUtc.AddSeconds(token.ExpiresInSeconds ?? DefaultTokenExpirySeconds).ToString("o")
        };

        var json = JsonSerializer.Serialize(credentialsData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_credentialsPath, json, ct);

        // Set restrictive file permissions (Unix/Linux/macOS)
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                // Set to 0600 (owner read/write only)
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"600 \"{_credentialsPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                });

                if (process != null)
                {
                    await process.WaitForExitAsync(ct);
                }
            }
            catch
            {
                // Ignore chmod errors on non-Unix systems or if chmod is not available
                // The file will still be created with default permissions
            }
        }
        // Note: On Windows, the file is created in user's profile directory which typically
        // has appropriate ACLs. For enhanced security, consider using FileSystemAclExtensions
        // with System.IO.FileSystem.AccessControl package in future versions.
    }

    /// <summary>
    /// Loads saved OAuth credentials from disk.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Token response loaded from the credentials file.</returns>
    private async Task<TokenResponse> LoadTokenResponseAsync(CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(_credentialsPath, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString();
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var tokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer";
        var scope = root.TryGetProperty("scope", out var s) ? s.GetString() : null;

        DateTime? issuedUtc = null;
        long? expiresInSeconds = null;

        if (root.TryGetProperty("expiry_date", out var expiryDate))
        {
            if (DateTime.TryParse(expiryDate.GetString(), out var expiry))
            {
                issuedUtc = DateTime.UtcNow;
                expiresInSeconds = (long)(expiry - issuedUtc.Value).TotalSeconds;
            }
        }

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = tokenType,
            Scope = scope,
            IssuedUtc = issuedUtc ?? DateTime.UtcNow,
            ExpiresInSeconds = expiresInSeconds
        };
    }

    public void Dispose()
    {
        _httpListener?.Stop();
        _httpListener?.Close();
    }
}
