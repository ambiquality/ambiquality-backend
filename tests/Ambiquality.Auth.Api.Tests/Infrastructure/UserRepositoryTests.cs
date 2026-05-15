using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Infrastructure.Persistence;

namespace Ambiquality.Auth.Api.Tests.Infrastructure;

[Collection(nameof(PostgresCollection))]
public class UserRepositoryTests(PostgresFixture fixture)
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Add_ThenGetByEmail_RoundTripsAggregateState()
    {
        var email = Email.Create($"roundtrip-{Guid.NewGuid():N}@example.com");
        var user = User.Register(email, "stored-hash", "confirm-hash", Now, TimeSpan.FromHours(24));

        await using (var writeContext = fixture.CreateContext())
        {
            var repository = new UserRepository(writeContext);
            repository.Add(user);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var loaded = await new UserRepository(readContext).GetByEmailAsync(email);

        Assert.NotNull(loaded);
        Assert.Equal(user.Id, loaded.Id);
        Assert.Equal(email.Value, loaded.Email.Value);
        Assert.False(loaded.EmailConfirmed);
        Assert.Equal("stored-hash", loaded.PasswordHash);
        var token = Assert.Single(loaded.VerificationTokens);
        Assert.Equal("confirm-hash", token.TokenHash);
        Assert.Equal(VerificationPurpose.EmailConfirmation, token.Purpose);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenUserDoesNotExist()
    {
        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);

        var loaded = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(loaded);
    }

    [Fact]
    public async Task ConfirmEmail_PersistsConfirmedStateAndConsumedToken()
    {
        var email = Email.Create($"confirm-{Guid.NewGuid():N}@example.com");
        var user = User.Register(email, "hash", "confirm-hash", Now, TimeSpan.FromHours(24));

        await using (var writeContext = fixture.CreateContext())
        {
            var repository = new UserRepository(writeContext);
            repository.Add(user);
            await repository.SaveChangesAsync();
        }

        await using (var updateContext = fixture.CreateContext())
        {
            var repository = new UserRepository(updateContext);
            var loaded = await repository.GetByIdAsync(user.Id);
            loaded!.ConfirmEmail("confirm-hash", Now.AddHours(1));
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var confirmed = await new UserRepository(readContext).GetByIdAsync(user.Id);

        Assert.NotNull(confirmed);
        Assert.True(confirmed.EmailConfirmed);
        Assert.NotNull(Assert.Single(confirmed.VerificationTokens).ConsumedAt);
    }

    [Fact]
    public async Task GetByRefreshTokenHash_FindsOwningUser()
    {
        var email = Email.Create($"refresh-{Guid.NewGuid():N}@example.com");
        var user = User.Register(email, "hash", "confirm-hash", Now, TimeSpan.FromHours(24));
        user.ConfirmEmail("confirm-hash", Now);
        var rtHash = $"rt-{Guid.NewGuid():N}";
        user.IssueRefreshToken(rtHash, Now, TimeSpan.FromDays(30));

        await using (var writeContext = fixture.CreateContext())
        {
            var repository = new UserRepository(writeContext);
            repository.Add(user);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var loaded = await new UserRepository(readContext).GetByRefreshTokenHashAsync(rtHash);

        Assert.NotNull(loaded);
        Assert.Equal(user.Id, loaded.Id);
        Assert.Contains(loaded.RefreshTokens, t => t.TokenHash == rtHash);
    }
}
