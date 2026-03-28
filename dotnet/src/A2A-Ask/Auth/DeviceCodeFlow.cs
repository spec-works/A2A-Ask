using Duende.IdentityModel.Client;
using A2A;

namespace A2AAsk.Auth;

/// <summary>
/// Implements the OAuth2 Device Code flow for CLI-based authentication
/// using Duende.IdentityModel for standards-compliant token requests.
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
        string? clientId = null;
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

        // Derive device auth URL if not explicit
        var tokenUri = new Uri(tokenUrl);
        string deviceAuthUrl;
        if (!string.IsNullOrEmpty(deviceAuthUrlStr))
            deviceAuthUrl = deviceAuthUrlStr;
        else
            deviceAuthUrl = DeriveDeviceAuthorizationUrl(tokenUri).ToString();

        var scopeString = scopes != null ? string.Join(" ", scopes.Keys) : "";

        // Step 1: Request device authorization using IdentityModel
        var deviceAuthResponse = await _httpClient.RequestDeviceAuthorizationAsync(
            new DeviceAuthorizationRequest
            {
                Address = deviceAuthUrl,
                ClientId = clientId ?? "a2a-ask-cli",
                Scope = scopeString
            }, cancellationToken);

        if (deviceAuthResponse.IsError)
            throw new InvalidOperationException(
                $"Device authorization failed: {deviceAuthResponse.Error} - {deviceAuthResponse.ErrorDescription}");

        // Step 2: Display instructions to user
        var verificationUri = deviceAuthResponse.VerificationUriComplete
            ?? deviceAuthResponse.VerificationUri;
        var userCode = deviceAuthResponse.UserCode;

        Console.WriteLine("To authenticate, open the following URL in your browser:");
        Console.WriteLine();
        Console.WriteLine($"  {verificationUri}");
        Console.WriteLine();
        Console.WriteLine($"Enter the code: {userCode}");
        Console.WriteLine();
        Console.WriteLine("Waiting for authentication...");

        // Step 3: Poll for token using IdentityModel
        var interval = deviceAuthResponse.Interval is > 0 ? (int)deviceAuthResponse.Interval : 5;
        var expiresIn = deviceAuthResponse.ExpiresIn is > 0 ? (double)deviceAuthResponse.ExpiresIn : 600.0;
        var deadline = DateTime.UtcNow.AddSeconds(expiresIn);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);

            var tokenResponse = await _httpClient.RequestDeviceTokenAsync(
                new DeviceTokenRequest
                {
                    Address = tokenUrl,
                    ClientId = clientId ?? "a2a-ask-cli",
                    DeviceCode = deviceAuthResponse.DeviceCode!
                }, cancellationToken);

            if (tokenResponse.IsError)
            {
                switch (tokenResponse.Error)
                {
                    case "authorization_pending":
                        continue;
                    case "slow_down":
                        interval += 5;
                        continue;
                    case "expired_token":
                        Console.Error.WriteLine("Device code expired. Please try again.");
                        return null;
                    case "access_denied":
                        Console.Error.WriteLine("Access denied by user.");
                        return null;
                    default:
                        Console.Error.WriteLine($"Authentication error: {tokenResponse.Error} - {tokenResponse.ErrorDescription}");
                        return null;
                }
            }

            // Success
            return new TokenResult
            {
                AccessToken = tokenResponse.AccessToken!,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresAt = tokenResponse.ExpiresIn > 0
                    ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                    : null,
                TokenType = tokenResponse.TokenType ?? "Bearer",
                TokenUrl = tokenUrl
            };
        }

        Console.Error.WriteLine("Authentication timed out.");
        return null;
    }

    /// <summary>
    /// Refreshes an expired token using the stored refresh token.
    /// </summary>
    public static async Task<TokenResult?> RefreshTokenAsync(
        TokenResult expiredToken, HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(expiredToken.RefreshToken) || string.IsNullOrEmpty(expiredToken.TokenUrl))
            return null;

        var client = httpClient ?? new HttpClient();
        var response = await client.RequestRefreshTokenAsync(
            new RefreshTokenRequest
            {
                Address = expiredToken.TokenUrl,
                ClientId = "a2a-ask-cli",
                RefreshToken = expiredToken.RefreshToken
            }, cancellationToken);

        if (response.IsError)
            return null;

        return new TokenResult
        {
            AccessToken = response.AccessToken!,
            RefreshToken = response.RefreshToken ?? expiredToken.RefreshToken,
            ExpiresAt = response.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(response.ExpiresIn)
                : null,
            TokenType = response.TokenType ?? "Bearer",
            TokenUrl = expiredToken.TokenUrl
        };
    }

    private static Uri DeriveDeviceAuthorizationUrl(Uri tokenUrl)
    {
        var path = tokenUrl.AbsolutePath;
        if (path.Contains("/token", StringComparison.OrdinalIgnoreCase))
        {
            var devicePath = path.Replace("/token", "/devicecode", StringComparison.OrdinalIgnoreCase);
            return new Uri(tokenUrl, devicePath);
        }
        return new Uri(tokenUrl, "./devicecode");
    }
}
