using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using A2A;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;

namespace A2AAsk.Auth;

/// <summary>
/// Implements the OAuth2 Authorization Code flow with PKCE for CLI authentication.
/// Opens a browser for user login and listens on a localhost callback URL.
/// </summary>
public static class AuthCodeFlow
{
    public static async Task<TokenResult?> AuthenticateAsync(
        OAuth2SecurityScheme scheme,
        IEnumerable<string>? requiredScopes = null,
        CancellationToken cancellationToken = default)
    {
        var authCodeFlow = scheme.Flows?.AuthorizationCode;
        if (authCodeFlow == null)
            throw new InvalidOperationException("No authorization code flow found in OAuth2 scheme.");

        var allScopes = new HashSet<string>();
        if (authCodeFlow.Scopes != null)
            foreach (var s in authCodeFlow.Scopes.Keys) allScopes.Add(s);
        if (requiredScopes != null)
            foreach (var s in requiredScopes) allScopes.Add(s);

        var options = new OidcClientOptions
        {
            Authority = new Uri(authCodeFlow.AuthorizationUrl).GetLeftPart(UriPartial.Authority),
            ClientId = "a2a-ask-cli",
            Scope = string.Join(" ", allScopes),
            RedirectUri = "http://127.0.0.1:29080/callback",
            Browser = new SystemBrowser(29080),
            Policy = new Policy { Discovery = new Duende.IdentityModel.Client.DiscoveryPolicy { RequireHttps = false } }
        };

        // If the scheme provides explicit endpoints, use them
        options.ProviderInformation = new ProviderInformation
        {
            IssuerName = options.Authority,
            AuthorizeEndpoint = authCodeFlow.AuthorizationUrl,
            TokenEndpoint = authCodeFlow.TokenUrl,
        };

        var client = new OidcClient(options);
        var result = await client.LoginAsync(cancellationToken: cancellationToken);

        if (result.IsError)
        {
            Console.Error.WriteLine($"Authorization code flow failed: {result.Error}");
            return null;
        }

        return new TokenResult
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresAt = result.AccessTokenExpiration != DateTimeOffset.MinValue
                ? result.AccessTokenExpiration.UtcDateTime
                : null,
            TokenType = "Bearer",
            TokenUrl = authCodeFlow.TokenUrl
        };
    }
}

/// <summary>
/// Opens the system browser and listens on a localhost port for the OAuth2 callback.
/// </summary>
internal class SystemBrowser : IBrowser
{
    private readonly int _port;

    public SystemBrowser(int port) => _port = port;

    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        listener.Start();

        OpenBrowser(options.StartUrl);

        try
        {
            var context = await listener.GetContextAsync().WaitAsync(
                TimeSpan.FromMinutes(5), cancellationToken);

            var url = context.Request.Url!.ToString();

            // Send a response to the browser
            var responseHtml = "<html><body><h1>Authentication complete</h1><p>You can close this window.</p></body></html>";
            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.ContentType = "text/html";
            await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
            context.Response.Close();

            return new BrowserResult
            {
                Response = url,
                ResultType = BrowserResultType.Success
            };
        }
        catch (OperationCanceledException)
        {
            return new BrowserResult
            {
                ResultType = BrowserResultType.Timeout,
                Error = "Authentication timed out."
            };
        }
        finally
        {
            listener.Stop();
        }
    }

    private static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
        else
            Process.Start("xdg-open", url);
    }
}
