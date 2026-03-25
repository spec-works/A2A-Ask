using System.Net.Http.Json;
using System.Text.Json;
using A2A;

namespace A2AAsk.Auth;

/// <summary>
/// Implements the OAuth2 Device Code flow for CLI-based authentication.
/// Uses the DeviceCode flow from OAuthFlows when available, otherwise derives
/// the device authorization endpoint from the token URL.
/// </summary>
public class DeviceCodeFlow
{
    private readonly OAuth2SecurityScheme _scheme;
    private readonly HttpClient _httpClient;

    public DeviceCodeFlow(OAuth2SecurityScheme scheme, HttpClient? httpClient = null)
    {
        _scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<TokenResult?> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        string? tokenUrl = null;
        string? deviceAuthUrlStr = null;
        IDictionary<string, string>? scopes = null;

        if (_scheme.Flows != null)
        {
            switch (_scheme.Flows.FlowCase)
            {
                case OAuthFlowCase.DeviceCode:
                    tokenUrl = _scheme.Flows.DeviceCode?.TokenUrl;
                    deviceAuthUrlStr = _scheme.Flows.DeviceCode?.DeviceAuthorizationUrl;
                    scopes = _scheme.Flows.DeviceCode?.Scopes;
                    break;
                case OAuthFlowCase.AuthorizationCode:
                    tokenUrl = _scheme.Flows.AuthorizationCode?.TokenUrl;
                    scopes = _scheme.Flows.AuthorizationCode?.Scopes;
                    break;
                case OAuthFlowCase.ClientCredentials:
                    tokenUrl = _scheme.Flows.ClientCredentials?.TokenUrl;
                    scopes = _scheme.Flows.ClientCredentials?.Scopes;
                    break;
            }
        }

        if (tokenUrl == null)
            throw new InvalidOperationException(
                "Cannot determine token URL from OAuth2 scheme. " +
                "The agent's OAuth2 configuration doesn't include device code, authorization code, or client credentials flows.");

        // If we don't have an explicit device auth URL, derive it
        var tokenUri = new Uri(tokenUrl);
        Uri deviceAuthUrl;
        if (!string.IsNullOrEmpty(deviceAuthUrlStr))
            deviceAuthUrl = new Uri(deviceAuthUrlStr);
        else
            deviceAuthUrl = DeriveDeviceAuthorizationUrl(tokenUri);

        var scopeString = scopes != null ? string.Join(" ", scopes.Keys) : "";

        // Step 1: Request device code
        var deviceRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["scope"] = scopeString
        });

        var deviceResponse = await _httpClient.PostAsync(deviceAuthUrl, deviceRequest, cancellationToken);
        deviceResponse.EnsureSuccessStatusCode();

        var deviceJson = await deviceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

        var userCode = deviceJson.GetProperty("user_code").GetString()!;
        var verificationUri = deviceJson.TryGetProperty("verification_uri_complete", out var complete)
            ? complete.GetString()!
            : deviceJson.GetProperty("verification_uri").GetString()!;
        var deviceCodeValue = deviceJson.GetProperty("device_code").GetString()!;
        var interval = deviceJson.TryGetProperty("interval", out var intervalProp)
            ? intervalProp.GetInt32()
            : 5;
        var expiresIn = deviceJson.TryGetProperty("expires_in", out var expiresProp)
            ? expiresProp.GetInt32()
            : 600;

        // Step 2: Display instructions to user
        Console.WriteLine("To authenticate, open the following URL in your browser:");
        Console.WriteLine();
        Console.WriteLine($"  {verificationUri}");
        Console.WriteLine();
        Console.WriteLine($"Enter the code: {userCode}");
        Console.WriteLine();
        Console.WriteLine("Waiting for authentication...");

        // Step 3: Poll for token
        var deadline = DateTime.UtcNow.AddSeconds(expiresIn);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);

            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["device_code"] = deviceCodeValue
            });

            var tokenResponse = await _httpClient.PostAsync(tokenUrl, tokenRequest, cancellationToken);

            if (tokenResponse.IsSuccessStatusCode)
            {
                var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                return new TokenResult
                {
                    AccessToken = tokenJson.GetProperty("access_token").GetString()!,
                    RefreshToken = tokenJson.TryGetProperty("refresh_token", out var refresh)
                        ? refresh.GetString()
                        : null,
                    ExpiresAt = tokenJson.TryGetProperty("expires_in", out var expiresInToken)
                        ? DateTime.UtcNow.AddSeconds(expiresInToken.GetInt32())
                        : null,
                    TokenType = tokenJson.TryGetProperty("token_type", out var tokenType)
                        ? tokenType.GetString()
                        : "Bearer"
                };
            }

            var errorJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var error = errorJson.TryGetProperty("error", out var errorProp)
                ? errorProp.GetString()
                : null;

            switch (error)
            {
                case "authorization_pending":
                    break;
                case "slow_down":
                    interval += 5;
                    break;
                case "expired_token":
                    Console.Error.WriteLine("Device code expired. Please try again.");
                    return null;
                case "access_denied":
                    Console.Error.WriteLine("Access denied by user.");
                    return null;
                default:
                    Console.Error.WriteLine($"Authentication error: {error}");
                    return null;
            }
        }

        Console.Error.WriteLine("Authentication timed out.");
        return null;
    }

    /// <summary>
    /// Derives the device authorization URL from the token URL.
    /// Common patterns: /oauth2/v2.0/devicecode, /oauth/device/code, etc.
    /// Falls back to replacing /token with /devicecode in the path.
    /// </summary>
    private static Uri DeriveDeviceAuthorizationUrl(Uri tokenUrl)
    {
        var path = tokenUrl.AbsolutePath;

        // Common pattern: replace /token with /devicecode
        if (path.Contains("/token", StringComparison.OrdinalIgnoreCase))
        {
            var devicePath = path.Replace("/token", "/devicecode", StringComparison.OrdinalIgnoreCase);
            return new Uri(tokenUrl, devicePath);
        }

        // Fallback: append /devicecode to the base
        return new Uri(tokenUrl, "./devicecode");
    }
}
