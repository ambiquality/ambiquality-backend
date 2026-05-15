using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Tests.TestSupport;
using NSubstitute;

namespace Ambiquality.Auth.Api.Tests.Application;

public class RefreshTokenHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserRepository _repository = new();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly IJwtIssuer _jwtIssuer = Substitute.For<IJwtIssuer>();
    private readonly FakeClock _clock = new(Now);
    private readonly AuthOptions _options = new();

    private RefreshTokenHandler CreateHandler() => new(
        _repository, _tokenGenerator, _jwtIssuer, _clock, _options);

    private User SeedUserWithRefreshToken()
    {
        var user = User.Register(
            Email.Create("user@example.com"), "hash", "confirm-hash",
            Now.AddDays(-1), TimeSpan.FromHours(24));
        user.ConfirmEmail("confirm-hash", Now.AddDays(-1));
        user.IssueRefreshToken("old-hash", Now.AddDays(-1), TimeSpan.FromDays(30));
        _repository.Add(user);
        return user;
    }

    [Fact]
    public async Task Handle_WithActiveToken_RotatesAndReturnsNewTokens()
    {
        var user = SeedUserWithRefreshToken();
        _tokenGenerator.Hash("old-raw").Returns("old-hash");
        _tokenGenerator.Generate().Returns(new GeneratedToken("new-raw", "new-hash"));
        _jwtIssuer.Issue(Arg.Any<User>())
            .Returns(new AccessToken("jwt-value", Now.AddMinutes(15)));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new RefreshTokenCommand("old-raw"));

        Assert.Equal("jwt-value", result.AccessToken);
        Assert.Equal("new-raw", result.RefreshToken);
        var old = user.RefreshTokens.Single(t => t.TokenHash == "old-hash");
        var fresh = user.RefreshTokens.Single(t => t.TokenHash == "new-hash");
        Assert.False(old.IsActive(Now));
        Assert.True(fresh.IsActive(Now));
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WithUnknownToken_ThrowsInvalidRefreshToken()
    {
        SeedUserWithRefreshToken();
        _tokenGenerator.Hash("bogus-raw").Returns("bogus-hash");
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() =>
            handler.HandleAsync(new RefreshTokenCommand("bogus-raw")));
    }

    [Fact]
    public async Task Handle_WithRevokedToken_ThrowsInvalidRefreshToken()
    {
        var user = SeedUserWithRefreshToken();
        user.RevokeAllRefreshTokens(Now.AddHours(-1));
        _tokenGenerator.Hash("old-raw").Returns("old-hash");
        _tokenGenerator.Generate().Returns(new GeneratedToken("new-raw", "new-hash"));
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() =>
            handler.HandleAsync(new RefreshTokenCommand("old-raw")));
    }
}
