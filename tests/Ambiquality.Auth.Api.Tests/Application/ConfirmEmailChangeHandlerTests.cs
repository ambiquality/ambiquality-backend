using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Tests.TestSupport;
using NSubstitute;

namespace Ambiquality.Auth.Api.Tests.Application;

public class ConfirmEmailChangeHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserRepository _repository = new();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly FakeClock _clock = new(Now);

    private ConfirmEmailChangeHandler CreateHandler() => new(_repository, _tokenGenerator, _clock);

    private User SeedUserWithPendingEmailChange()
    {
        var user = User.Register(
            Email.Create("user@example.com"), "hash", "confirm-hash",
            Now.AddDays(-1), TimeSpan.FromHours(24));
        user.ConfirmEmail("confirm-hash", Now.AddDays(-1));
        user.RequestEmailChange(
            Email.Create("new@example.com"), "change-hash", Now.AddHours(-1), TimeSpan.FromHours(24));
        _repository.Add(user);
        return user;
    }

    [Fact]
    public async Task Handle_WithValidToken_AppliesPendingEmail()
    {
        var user = SeedUserWithPendingEmailChange();
        _tokenGenerator.Hash("change-raw").Returns("change-hash");
        var handler = CreateHandler();

        await handler.HandleAsync(new ConfirmEmailChangeCommand(user.Id, "change-raw"));

        Assert.Equal("new@example.com", user.Email.Value);
        Assert.Null(user.PendingEmail);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsUserNotFound()
    {
        _tokenGenerator.Hash(Arg.Any<string>()).Returns("change-hash");
        var handler = CreateHandler();

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.HandleAsync(new ConfirmEmailChangeCommand(Guid.NewGuid(), "change-raw")));
    }

    [Fact]
    public async Task Handle_WithWrongToken_ThrowsDomainException()
    {
        var user = SeedUserWithPendingEmailChange();
        _tokenGenerator.Hash("bad-raw").Returns("bad-hash");
        var handler = CreateHandler();

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new ConfirmEmailChangeCommand(user.Id, "bad-raw")));
        Assert.Equal("user@example.com", user.Email.Value);
    }
}
