using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Infrastructure.Security;

namespace Ambiquality.Auth.Api.Tests.Infrastructure;

public class IdentityPasswordHasherTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly IdentityPasswordHasher _passwordService = new();

    private static User CreateUser() => User.Register(
        Email.Create("user@example.com"), "placeholder", "confirm-hash",
        Now, TimeSpan.FromHours(24));

    [Fact]
    public void Hash_ProducesNonEmptyHashDifferentFromPlaintext()
    {
        var user = CreateUser();

        var hash = _passwordService.Hash(user, "Sup3rSecret!");

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.NotEqual("Sup3rSecret!", hash);
    }

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        var user = CreateUser();
        var hash = _passwordService.Hash(user, "Sup3rSecret!");

        Assert.True(_passwordService.Verify(user, hash, "Sup3rSecret!"));
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var user = CreateUser();
        var hash = _passwordService.Hash(user, "Sup3rSecret!");

        Assert.False(_passwordService.Verify(user, hash, "wrong-password"));
    }

    [Fact]
    public void Hash_IsSaltedSoSameInputYieldsDifferentHashes()
    {
        var user = CreateUser();

        var first = _passwordService.Hash(user, "Sup3rSecret!");
        var second = _passwordService.Hash(user, "Sup3rSecret!");

        Assert.NotEqual(first, second);
    }
}
