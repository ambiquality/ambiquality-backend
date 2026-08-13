using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Tests.TestSupport;
using NSubstitute;

namespace Ambiquality.Auth.Api.Tests.Application;

public class ChangePasswordHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserRepository _repository = new();
    private readonly IPasswordService _passwordService = Substitute.For<IPasswordService>();
    private readonly FakeClock _clock = new(Now);
    private readonly AuthOptions _options = new();

    private ChangePasswordHandler CreateHandler() => new(_repository, _passwordService, _clock, _options);

    private User SeedConfirmedUserWithRefreshToken()
    {
        var user = User.Register(
            Email.Create("user@example.com"), "old-hash", "confirm-hash",
            Now.AddDays(-1), TimeSpan.FromHours(24));
        user.ConfirmEmail("confirm-hash", Now.AddDays(-1));
        user.IssueRefreshToken("rt-hash", Now.AddHours(-1), TimeSpan.FromDays(30));
        _repository.Add(user);
        return user;
    }

    [Fact]
    public async Task Handle_WithCorrectCurrentPassword_UpdatesHash()
    {
        var user = SeedConfirmedUserWithRefreshToken();
        _passwordService.Verify(user, "old-hash", "current-pw").Returns(true);
        _passwordService.Hash(user, "new-S3cret-pass").Returns("new-hash");
        var handler = CreateHandler();

        await handler.HandleAsync(new ChangePasswordCommand(user.Id, "current-pw", "new-S3cret-pass"));

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_RevokesAllRefreshTokens()
    {
        var user = SeedConfirmedUserWithRefreshToken();
        _passwordService.Verify(user, "old-hash", "current-pw").Returns(true);
        _passwordService.Hash(user, "new-S3cret-pass").Returns("new-hash");
        var handler = CreateHandler();

        await handler.HandleAsync(new ChangePasswordCommand(user.Id, "current-pw", "new-S3cret-pass"));

        Assert.All(user.RefreshTokens, t => Assert.False(t.IsActive(Now)));
    }

    [Fact]
    public async Task Handle_WithTooShortNewPassword_ThrowsWeakPassword_AndKeepsOldHash()
    {
        var user = SeedConfirmedUserWithRefreshToken();
        _passwordService.Verify(user, "old-hash", "current-pw").Returns(true);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<WeakPasswordException>(() =>
            handler.HandleAsync(new ChangePasswordCommand(user.Id, "current-pw", "short")));

        Assert.Equal("old-hash", user.PasswordHash);
    }

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_ThrowsInvalidCredentials()
    {
        var user = SeedConfirmedUserWithRefreshToken();
        _passwordService.Verify(user, Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new ChangePasswordCommand(user.Id, "wrong-pw", "new-S3cret-pass")));
        Assert.Equal("old-hash", user.PasswordHash);
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsUserNotFound()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.HandleAsync(new ChangePasswordCommand(Guid.NewGuid(), "current-pw", "new-S3cret-pass")));
    }
}
