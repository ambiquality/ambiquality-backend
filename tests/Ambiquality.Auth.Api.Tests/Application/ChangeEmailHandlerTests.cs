using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Tests.TestSupport;
using NSubstitute;

namespace Ambiquality.Auth.Api.Tests.Application;

public class ChangeEmailHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserRepository _repository = new();
    private readonly IPasswordService _passwordService = Substitute.For<IPasswordService>();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly FakeClock _clock = new(Now);
    private readonly AuthOptions _options = new();

    private ChangeEmailHandler CreateHandler()
    {
        _tokenGenerator.Generate().Returns(new GeneratedToken("change-raw", "change-hash"));
        return new ChangeEmailHandler(
            _repository, _passwordService, _tokenGenerator, _emailSender, _clock, _options);
    }

    private User SeedConfirmedUser()
    {
        var user = User.Register(
            Email.Create("user@example.com"), "hash", "confirm-hash",
            Now.AddDays(-1), TimeSpan.FromHours(24));
        user.ConfirmEmail("confirm-hash", Now.AddDays(-1));
        _repository.Add(user);
        return user;
    }

    [Fact]
    public async Task Handle_SetsPendingEmailAndSendsTokenToNewAddress()
    {
        var user = SeedConfirmedUser();
        _passwordService.Verify(user, "hash", "correct-pw").Returns(true);
        var handler = CreateHandler();

        await handler.HandleAsync(new ChangeEmailCommand(user.Id, "correct-pw", "New@Example.com"));

        Assert.Equal("new@example.com", user.PendingEmail!.Value);
        await _emailSender.Received(1).SendAsync(
            "new@example.com", Arg.Any<string>(),
            Arg.Is<string>(b => b.Contains("change-raw")), Arg.Any<CancellationToken>());
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_ThrowsInvalidCredentials()
    {
        var user = SeedConfirmedUser();
        _passwordService.Verify(user, Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new ChangeEmailCommand(user.Id, "wrong-pw", "new@example.com")));
        Assert.Null(user.PendingEmail);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsUserNotFound()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.HandleAsync(new ChangeEmailCommand(Guid.NewGuid(), "any-pw", "new@example.com")));
    }

    [Fact]
    public async Task Handle_WithInvalidNewEmail_ThrowsInvalidEmail()
    {
        var user = SeedConfirmedUser();
        _passwordService.Verify(user, "hash", "correct-pw").Returns(true);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidEmailException>(() =>
            handler.HandleAsync(new ChangeEmailCommand(user.Id, "correct-pw", "not-an-email")));
    }
}
