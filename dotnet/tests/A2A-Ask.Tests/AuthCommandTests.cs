using A2A;
using A2AAsk.Commands;

namespace A2AAsk.Tests;

public class AuthCommandTests
{
    [Fact]
    public void ExtractIssuerFromOAuth2Scheme_UsesDiscoveredIssuer()
    {
        var scheme = new OAuth2SecurityScheme
        {
            OAuth2MetadataUrl = "https://login.example.com/common/v2.0/.well-known/openid-configuration"
        };

        var issuer = AuthCommand.ExtractIssuerFromOAuth2Scheme(
            scheme,
            "https://login.example.com/Tenant/v2.0/");

        Assert.Equal("https://login.example.com/tenant/v2.0", issuer);
    }

    [Fact]
    public void ExtractIssuerFromOAuth2Scheme_DerivesFromMetadataUrl()
    {
        var scheme = new OAuth2SecurityScheme
        {
            OAuth2MetadataUrl = "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration"
        };

        var issuer = AuthCommand.ExtractIssuerFromOAuth2Scheme(scheme);

        Assert.Equal("https://login.microsoftonline.com/common/v2.0", issuer);
    }

    [Fact]
    public void ExtractIssuerFromOAuth2Scheme_FallsBackToTokenAuthority()
    {
        var scheme = new OAuth2SecurityScheme
        {
            Flows = new OAuthFlows
            {
                ClientCredentials = new ClientCredentialsOAuthFlow
                {
                    TokenUrl = "https://auth.example.com/oauth2/v2.0/token"
                }
            }
        };

        var issuer = AuthCommand.ExtractIssuerFromOAuth2Scheme(scheme);

        Assert.Equal("https://auth.example.com", issuer);
    }
}
