using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Tests.TestSupport;
using NSubstitute;

namespace Ambiquality.Auth.Api.Tests.Application;

public class RegisterUserHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserRepository _repository = new();
    private readonly IPasswordService _passwordService = Substitute.For<IPasswordService>();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly FakeClock _clock = new(Now);
    private readonly AuthOptions _options = new();

    private RegisterUserHandler CreateHandler()
    {
        _passwordService.Hash(Arg.Any<User>(), Arg.Any<string>()).Returns("hashed-pw");
        _tokenGenerator.Generate().Returns(new GeneratedToken("raw-token", "token-hash"));
        return new RegisterUserHandler(
            _repository, _passwordService, _tokenGenerator, _emailSender, _clock, _options);
    }

    [Fact]
    public async Task Handle_PersistsUnconfirmedUserWithHashedPassword()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(new RegisterUserCommand("New@Example.com", "Sup3rSecret!"));

        var user = Assert.Single(_repository.Users);
        Assert.Equal("new@example.com", user.Email.Value);
        Assert.False(user.EmailConfirmed);
        Assert.Equal("hashed-pw", user.PasswordHash);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_SendsConfirmationEmailWithRawToken()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(new RegisterUserCommand("new@example.com", "Sup3rSecret!"));

        await _emailSender.Received(1).SendAsync(
            "new@example.com",
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains("raw-token")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DoesNotIssueRefreshToken()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(new RegisterUserCommand("new@example.com", "Sup3rSecret!"));

        var user = Assert.Single(_repository.Users);
        Assert.Empty(user.RefreshTokens);
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_Throws()
    {
        var handler = CreateHandler();
        await handler.HandleAsync(new RegisterUserCommand("dup@example.com", "Sup3rSecret!"));

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() =>
            handler.HandleAsync(new RegisterUserCommand("dup@example.com", "An0therSecret!")));
    }

    [Fact]
    public async Task Handle_WithTooShortPassword_ThrowsWeakPassword_AndDoesNotPersist()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<WeakPasswordException>(() =>
            handler.HandleAsync(new RegisterUserCommand("new@example.com", "short")));

        Assert.Empty(_repository.Users);
    }

    [Fact]
    public async Task Handle_WithTooLongPassword_ThrowsWeakPassword_AndDoesNotPersist()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<WeakPasswordException>(() =>
            handler.HandleAsync(new RegisterUserCommand("new@example.com", new string('x', 129))));

        Assert.Empty(_repository.Users);
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ThrowsInvalidEmail()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidEmailException>(() =>
            handler.HandleAsync(new RegisterUserCommand("not-an-email", "Sup3rSecret!")));
    }
}
