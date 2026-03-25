using A2AAsk.Auth;

namespace A2AAsk.Tests;

public class TokenResultTests
{
    [Fact]
    public void IsExpired_ReturnsFalse_WhenExpiresAtInFuture()
    {
        var token = new TokenResult
        {
            AccessToken = "test",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        Assert.False(token.IsExpired);
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenExpiresAtInPast()
    {
        var token = new TokenResult
        {
            AccessToken = "test",
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        Assert.True(token.IsExpired);
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenExpiresAtIsNull()
    {
        var token = new TokenResult
        {
            AccessToken = "test",
            ExpiresAt = null
        };

        Assert.False(token.IsExpired);
    }
}
