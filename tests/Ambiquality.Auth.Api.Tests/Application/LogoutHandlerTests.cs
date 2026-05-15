using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Tests.TestSupport;

namespace Ambiquality.Auth.Api.Tests.Application;

public class LogoutHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserRepository _repository = new();
    private readonly FakeClock _clock = new(Now);

    private LogoutHandler CreateHandler() => new(_repository, _clock);

    private User SeedConfirmedUserWithToken(string tokenHash)
    {
        var user = User.Register(
            Email.Create("user@example.com"), "hash", "confirm-hash",
            Now.AddDays(-1), TimeSpan.FromHours(24));
        user.ConfirmEmail("confirm-hash", Now.AddDays(-1));
        user.IssueRefreshToken(tokenHash, Now.AddDays(-1), TimeSpan.FromDays(30));
        _repository.Add(user);
        return user;
    }

    [Fact]
    public async Task Handle_WithKnownUser_RevokesAllRefreshTokens()
    {
        var user = SeedConfirmedUserWithToken("rt-hash");
        var handler = CreateHandler();

        await handler.HandleAsync(new LogoutCommand(user.Id));

        Assert.All(user.RefreshTokens, t => Assert.False(t.IsActive(Now)));
    }

    [Fact]
    public async Task Handle_WithKnownUser_CallsSaveChanges()
    {
        var user = SeedConfirmedUserWithToken("rt-hash");
        var handler = CreateHandler();

        await handler.HandleAsync(new LogoutCommand(user.Id));

        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WithUnknownUserId_DoesNotThrow()
    {
        var handler = CreateHandler();

        var exception = await Record.ExceptionAsync(() =>
            handler.HandleAsync(new LogoutCommand(Guid.NewGuid())));

        Assert.Null(exception);
    }
}
