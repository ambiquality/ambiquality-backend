using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Tests.TestSupport;
using NSubstitute;

namespace Ambiquality.Auth.Api.Tests.Application;

public class ResendConfirmationHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserRepository _repository = new();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly FakeClock _clock = new(Now);
    private readonly AuthOptions _options = new();

    private ResendConfirmationHandler CreateHandler()
    {
        _tokenGenerator.Generate().Returns(new GeneratedToken("new-raw", "new-hash"));
        return new ResendConfirmationHandler(
            _repository, _tokenGenerator, _emailSender, _clock, _options);
    }

    [Fact]
    public async Task Handle_ForUnconfirmedUser_IssuesNewTokenAndSendsEmail()
    {
        var user = User.Register(
            Email.Create("user@example.com"), "hash", "confirm-hash",
            Now.AddMinutes(-5), TimeSpan.FromHours(24));
        _repository.Add(user);
        var handler = CreateHandler();

        await handler.HandleAsync(new ResendConfirmationCommand("user@example.com"));

        Assert.Contains(user.VerificationTokens, t => t.TokenHash == "new-hash");
        await _emailSender.Received(1).SendAsync(
            "user@example.com", Arg.Any<string>(),
            Arg.Is<string>(b => b.Contains("new-raw")), Arg.Any<CancellationToken>());
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_ForUnknownEmail_DoesNothing_NoEnumeration()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(new ResendConfirmationCommand("nobody@example.com"));

        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ForAlreadyConfirmedUser_DoesNotSendEmail()
    {
        var user = User.Register(
            Email.Create("user@example.com"), "hash", "confirm-hash",
            Now.AddMinutes(-5), TimeSpan.FromHours(24));
        user.ConfirmEmail("confirm-hash", Now.AddMinutes(-4));
        _repository.Add(user);
        var handler = CreateHandler();

        await handler.HandleAsync(new ResendConfirmationCommand("user@example.com"));

        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
