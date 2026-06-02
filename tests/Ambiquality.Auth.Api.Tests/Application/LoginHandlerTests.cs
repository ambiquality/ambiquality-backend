using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Tests.TestSupport;
using NSubstitute;

namespace Ambiquality.Auth.Api.Tests.Application;

public class LoginHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserRepository _repository = new();
    private readonly IPasswordService _passwordService = Substitute.For<IPasswordService>();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly IJwtIssuer _jwtIssuer = Substitute.For<IJwtIssuer>();
    private readonly FakeClock _clock = new(Now);
    private readonly FakeThrottleDelayer _throttleDelayer = new();
    private readonly AuthOptions _options = new();

    private LoginHandler CreateHandler() => new(
        _repository, _passwordService, _tokenGenerator, _jwtIssuer, _clock, _throttleDelayer, _options);

    private User SeedUser(bool confirmed)
    {
        var user = User.Register(
            Email.Create("user@example.com"), "stored-hash", "confirm-hash",
            Now.AddDays(-1), TimeSpan.FromHours(24));
        if (confirmed)
            user.ConfirmEmail("confirm-hash", Now.AddDays(-1));
        _repository.Add(user);
        return user;
    }

    [Fact]
    public async Task Handle_WithValidConfirmedCredentials_ReturnsTokens()
    {
        SeedUser(confirmed: true);
        _passwordService.Verify(Arg.Any<User>(), "stored-hash", "correct-pw").Returns(true);
        _jwtIssuer.Issue(Arg.Any<User>())
            .Returns(new AccessToken("jwt-value", Now.AddMinutes(15)));
        _tokenGenerator.Generate().Returns(new GeneratedToken("rt-raw", "rt-hash"));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new LoginCommand("user@example.com", "correct-pw"));

        Assert.Equal("jwt-value", result.AccessToken);
        Assert.Equal("rt-raw", result.RefreshToken);
        Assert.Equal(Now.AddDays(30), result.RefreshTokenExpiresAt);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_PersistsRefreshTokenHashOnUser()
    {
        var user = SeedUser(confirmed: true);
        _passwordService.Verify(Arg.Any<User>(), "stored-hash", "correct-pw").Returns(true);
        _jwtIssuer.Issue(Arg.Any<User>())
            .Returns(new AccessToken("jwt-value", Now.AddMinutes(15)));
        _tokenGenerator.Generate().Returns(new GeneratedToken("rt-raw", "rt-hash"));
        var handler = CreateHandler();

        await handler.HandleAsync(new LoginCommand("user@example.com", "correct-pw"));

        Assert.Contains(user.RefreshTokens, t => t.TokenHash == "rt-hash");
    }

    [Fact]
    public async Task Handle_WithUnknownEmail_ThrowsInvalidCredentials()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new LoginCommand("nobody@example.com", "whatever")));
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ThrowsInvalidCredentials()
    {
        SeedUser(confirmed: true);
        _passwordService.Verify(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new LoginCommand("user@example.com", "wrong-pw")));
    }

    [Fact]
    public async Task Handle_WhenEmailNotConfirmed_ThrowsEmailNotConfirmed()
    {
        SeedUser(confirmed: false);
        _passwordService.Verify(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<EmailNotConfirmedException>(() =>
            handler.HandleAsync(new LoginCommand("user@example.com", "correct-pw")));
    }

    [Fact]
    public async Task Handle_FailedLogin_RecordsFailureAndPersists()
    {
        var user = SeedUser(confirmed: true);
        _passwordService.Verify(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new LoginCommand("user@example.com", "wrong-pw")));

        Assert.Equal(1, user.FailedLoginCount);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WithinFreeAttempts_AppliesNoDelay()
    {
        SeedUser(confirmed: true);
        _passwordService.Verify(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var handler = CreateHandler();

        // The first failures (up to the free budget) must not be throttled.
        for (var i = 0; i < _options.LoginThrottleFreeAttempts; i++)
            await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
                handler.HandleAsync(new LoginCommand("user@example.com", "wrong-pw")));

        Assert.All(_throttleDelayer.Delays, d => Assert.Equal(TimeSpan.Zero, d));
    }

    [Fact]
    public async Task Handle_AfterFreeAttemptsExhausted_AppliesProgressiveDelay()
    {
        SeedUser(confirmed: true);
        _passwordService.Verify(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var handler = CreateHandler();

        // One more failure than the free budget, so the next attempt is delayed.
        for (var i = 0; i < _options.LoginThrottleFreeAttempts + 1; i++)
            await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
                handler.HandleAsync(new LoginCommand("user@example.com", "wrong-pw")));

        Assert.True(_throttleDelayer.LastDelay > TimeSpan.Zero);
        Assert.True(_throttleDelayer.LastDelay <= _options.LoginThrottleMaxDelay);
    }

    [Fact]
    public async Task Handle_SuccessfulLogin_ResetsFailureStreak()
    {
        var user = SeedUser(confirmed: true);
        _passwordService.Verify(Arg.Any<User>(), "stored-hash", "wrong-pw").Returns(false);
        _passwordService.Verify(Arg.Any<User>(), "stored-hash", "correct-pw").Returns(true);
        _jwtIssuer.Issue(Arg.Any<User>()).Returns(new AccessToken("jwt-value", Now.AddMinutes(15)));
        _tokenGenerator.Generate().Returns(new GeneratedToken("rt-raw", "rt-hash"));
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new LoginCommand("user@example.com", "wrong-pw")));
        Assert.Equal(1, user.FailedLoginCount);

        await handler.HandleAsync(new LoginCommand("user@example.com", "correct-pw"));

        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LastFailedLoginAt);
    }
}
