using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Tests.Domain.Users;

public class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Issue_CreatesActiveToken()
    {
        var token = RefreshToken.Issue("hash-abc", Now, TimeSpan.FromDays(30));

        Assert.Equal("hash-abc", token.TokenHash);
        Assert.Equal(Now, token.CreatedAt);
        Assert.Equal(Now.AddDays(30), token.ExpiresAt);
        Assert.Null(token.RevokedAt);
        Assert.True(token.IsActive(Now));
    }

    [Fact]
    public void IsActive_AfterExpiry_ReturnsFalse()
    {
        var token = RefreshToken.Issue("hash-abc", Now, TimeSpan.FromDays(30));

        Assert.False(token.IsActive(Now.AddDays(31)));
    }

    [Fact]
    public void IsActive_AtExpiryInstant_ReturnsFalse()
    {
        var token = RefreshToken.Issue("hash-abc", Now, TimeSpan.FromDays(30));

        Assert.False(token.IsActive(Now.AddDays(30)));
    }

    [Fact]
    public void Revoke_MakesTokenInactive()
    {
        var token = RefreshToken.Issue("hash-abc", Now, TimeSpan.FromDays(30));

        token.Revoke(Now.AddDays(1));

        Assert.Equal(Now.AddDays(1), token.RevokedAt);
        Assert.False(token.IsActive(Now.AddDays(2)));
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_DoesNotOverwriteTimestamp()
    {
        var token = RefreshToken.Issue("hash-abc", Now, TimeSpan.FromDays(30));
        token.Revoke(Now.AddDays(1));

        token.Revoke(Now.AddDays(2));

        Assert.Equal(Now.AddDays(1), token.RevokedAt);
    }
}
