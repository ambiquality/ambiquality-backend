using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Tests.TestSupport;
using NSubstitute;

namespace Ambiquality.Auth.Api.Tests.Application;

public class DeleteAccountHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserRepository _repository = new();
    private readonly IPasswordService _passwordService = Substitute.For<IPasswordService>();

    private DeleteAccountHandler CreateHandler() => new(_repository, _passwordService);

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
    public async Task Handle_WithCorrectPassword_RemovesUser()
    {
        var user = SeedConfirmedUser();
        _passwordService.Verify(user, "hash", "current-pw").Returns(true);
        var handler = CreateHandler();

        await handler.HandleAsync(new DeleteAccountCommand(user.Id, "current-pw"));

        Assert.DoesNotContain(user, _repository.Users);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ThrowsInvalidCredentialsAndKeepsUser()
    {
        var user = SeedConfirmedUser();
        _passwordService.Verify(user, Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new DeleteAccountCommand(user.Id, "wrong-pw")));

        Assert.Contains(user, _repository.Users);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsUserNotFound()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.HandleAsync(new DeleteAccountCommand(Guid.NewGuid(), "current-pw")));
    }
}
