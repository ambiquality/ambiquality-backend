using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Tests.TestSupport;
using NSubstitute;

namespace Ambiquality.Auth.Api.Tests.Application;

public class ConfirmEmailHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserRepository _repository = new();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly FakeClock _clock = new(Now);

    private ConfirmEmailHandler CreateHandler() => new(_repository, _tokenGenerator, _clock);

    private User SeedUnconfirmedUser()
    {
        var user = User.Register(
            Email.Create("user@example.com"), "hash", "confirm-hash",
            Now.AddMinutes(-5), TimeSpan.FromHours(24));
        _repository.Add(user);
        return user;
    }

    [Fact]
    public async Task Handle_WithValidToken_ConfirmsEmail()
    {
        var user = SeedUnconfirmedUser();
        _tokenGenerator.Hash("raw-token").Returns("confirm-hash");
        var handler = CreateHandler();

        await handler.HandleAsync(new ConfirmEmailCommand(user.Id, "raw-token"));

        Assert.True(user.EmailConfirmed);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsUserNotFound()
    {
        _tokenGenerator.Hash(Arg.Any<string>()).Returns("confirm-hash");
        var handler = CreateHandler();

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.HandleAsync(new ConfirmEmailCommand(Guid.NewGuid(), "raw-token")));
    }

    [Fact]
    public async Task Handle_WithWrongToken_ThrowsDomainException()
    {
        var user = SeedUnconfirmedUser();
        _tokenGenerator.Hash("bad-token").Returns("bad-hash");
        var handler = CreateHandler();

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new ConfirmEmailCommand(user.Id, "bad-token")));
        Assert.False(user.EmailConfirmed);
    }
}
