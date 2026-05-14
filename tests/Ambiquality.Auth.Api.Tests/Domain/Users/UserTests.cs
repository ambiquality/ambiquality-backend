using Ambiquality.Auth.Api.Domain;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Tests.Domain.Users;

public class UserTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    private static User RegisterUser(out VerificationToken confirmation)
    {
        var user = User.Register(
            Email.Create("user@example.com"),
            passwordHash: "hashed-pw",
            confirmationTokenHash: "confirm-hash",
            now: Now,
            confirmationTokenLifetime: TokenLifetime);
        confirmation = Assert.Single(user.VerificationTokens);
        return user;
    }

    [Fact]
    public void Register_CreatesUnconfirmedUserWithConfirmationToken()
    {
        var user = RegisterUser(out var confirmation);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("user@example.com", user.Email.Value);
        Assert.False(user.EmailConfirmed);
        Assert.Equal("hashed-pw", user.PasswordHash);
        Assert.Null(user.PendingEmail);
        Assert.Equal(VerificationPurpose.EmailConfirmation, confirmation.Purpose);
        Assert.Equal("confirm-hash", confirmation.TokenHash);
    }

    [Fact]
    public void ConfirmEmail_WithValidToken_SetsEmailConfirmed()
    {
        var user = RegisterUser(out _);

        user.ConfirmEmail("confirm-hash", Now.AddHours(1));

        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public void ConfirmEmail_WithWrongToken_Throws()
    {
        var user = RegisterUser(out _);

        Assert.Throws<DomainException>(() => user.ConfirmEmail("wrong-hash", Now.AddHours(1)));
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public void ConfirmEmail_WhenExpired_Throws()
    {
        var user = RegisterUser(out _);

        Assert.Throws<DomainException>(() => user.ConfirmEmail("confirm-hash", Now.AddHours(25)));
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public void ConfirmEmail_WhenAlreadyConfirmed_Throws()
    {
        var user = RegisterUser(out _);
        user.ConfirmEmail("confirm-hash", Now.AddHours(1));

        Assert.Throws<DomainException>(() => user.ConfirmEmail("confirm-hash", Now.AddHours(2)));
    }

    [Fact]
    public void ConfirmEmail_ConsumesToken_SoItCannotBeReused()
    {
        var user = RegisterUser(out var confirmation);

        user.ConfirmEmail("confirm-hash", Now.AddHours(1));

        Assert.NotNull(confirmation.ConsumedAt);
    }

    [Fact]
    public void AddConfirmationToken_WhenAlreadyConfirmed_Throws()
    {
        var user = RegisterUser(out _);
        user.ConfirmEmail("confirm-hash", Now.AddHours(1));

        Assert.Throws<DomainException>(() =>
            user.AddConfirmationToken("new-hash", Now.AddHours(2), TokenLifetime));
    }

    [Fact]
    public void AddConfirmationToken_WhenUnconfirmed_AddsNewToken()
    {
        var user = RegisterUser(out _);

        user.AddConfirmationToken("new-hash", Now.AddHours(2), TokenLifetime);

        user.ConfirmEmail("new-hash", Now.AddHours(3));
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public void ChangePassword_ReplacesHash()
    {
        var user = RegisterUser(out _);

        user.ChangePassword("new-hash");

        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Fact]
    public void IssueRefreshToken_AddsActiveToken()
    {
        var user = RegisterUser(out _);

        user.IssueRefreshToken("rt-hash", Now, RefreshLifetime);

        var token = Assert.Single(user.RefreshTokens);
        Assert.True(token.IsActive(Now));
        Assert.Equal("rt-hash", token.TokenHash);
    }

    [Fact]
    public void RotateRefreshToken_RevokesOldAndIssuesNew()
    {
        var user = RegisterUser(out _);
        user.IssueRefreshToken("old-hash", Now, RefreshLifetime);

        user.RotateRefreshToken("old-hash", "new-hash", Now.AddDays(1), RefreshLifetime);

        var old = user.RefreshTokens.Single(t => t.TokenHash == "old-hash");
        var fresh = user.RefreshTokens.Single(t => t.TokenHash == "new-hash");
        Assert.False(old.IsActive(Now.AddDays(1)));
        Assert.True(fresh.IsActive(Now.AddDays(1)));
    }

    [Fact]
    public void RotateRefreshToken_WithUnknownToken_Throws()
    {
        var user = RegisterUser(out _);
        user.IssueRefreshToken("old-hash", Now, RefreshLifetime);

        Assert.Throws<DomainException>(() =>
            user.RotateRefreshToken("unknown", "new-hash", Now.AddDays(1), RefreshLifetime));
    }

    [Fact]
    public void RotateRefreshToken_WithRevokedToken_Throws()
    {
        var user = RegisterUser(out _);
        user.IssueRefreshToken("old-hash", Now, RefreshLifetime);
        user.RevokeAllRefreshTokens(Now.AddHours(1));

        Assert.Throws<DomainException>(() =>
            user.RotateRefreshToken("old-hash", "new-hash", Now.AddDays(1), RefreshLifetime));
    }

    [Fact]
    public void RotateRefreshToken_WithExpiredToken_Throws()
    {
        var user = RegisterUser(out _);
        user.IssueRefreshToken("old-hash", Now, RefreshLifetime);

        Assert.Throws<DomainException>(() =>
            user.RotateRefreshToken("old-hash", "new-hash", Now.AddDays(31), RefreshLifetime));
    }

    [Fact]
    public void RevokeAllRefreshTokens_DeactivatesEveryToken()
    {
        var user = RegisterUser(out _);
        user.IssueRefreshToken("a", Now, RefreshLifetime);
        user.IssueRefreshToken("b", Now, RefreshLifetime);

        user.RevokeAllRefreshTokens(Now.AddHours(1));

        Assert.All(user.RefreshTokens, t => Assert.False(t.IsActive(Now.AddHours(2))));
    }

    [Fact]
    public void RequestEmailChange_SetsPendingEmailAndIssuesToken()
    {
        var user = RegisterUser(out _);
        user.ConfirmEmail("confirm-hash", Now.AddHours(1));

        user.RequestEmailChange(
            Email.Create("new@example.com"), "change-hash", Now.AddHours(2), TokenLifetime);

        Assert.Equal("new@example.com", user.PendingEmail!.Value);
        Assert.Contains(user.VerificationTokens,
            t => t.Purpose == VerificationPurpose.EmailChange && t.TokenHash == "change-hash");
    }

    [Fact]
    public void RequestEmailChange_WhenEmailNotConfirmed_Throws()
    {
        var user = RegisterUser(out _);

        Assert.Throws<DomainException>(() => user.RequestEmailChange(
            Email.Create("new@example.com"), "change-hash", Now.AddHours(2), TokenLifetime));
    }

    [Fact]
    public void RequestEmailChange_ToSameEmail_Throws()
    {
        var user = RegisterUser(out _);
        user.ConfirmEmail("confirm-hash", Now.AddHours(1));

        Assert.Throws<DomainException>(() => user.RequestEmailChange(
            Email.Create("user@example.com"), "change-hash", Now.AddHours(2), TokenLifetime));
    }

    [Fact]
    public void ConfirmEmailChange_WithValidToken_AppliesPendingEmail()
    {
        var user = RegisterUser(out _);
        user.ConfirmEmail("confirm-hash", Now.AddHours(1));
        user.RequestEmailChange(
            Email.Create("new@example.com"), "change-hash", Now.AddHours(2), TokenLifetime);

        user.ConfirmEmailChange("change-hash", Now.AddHours(3));

        Assert.Equal("new@example.com", user.Email.Value);
        Assert.Null(user.PendingEmail);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public void ConfirmEmailChange_WithWrongToken_Throws()
    {
        var user = RegisterUser(out _);
        user.ConfirmEmail("confirm-hash", Now.AddHours(1));
        user.RequestEmailChange(
            Email.Create("new@example.com"), "change-hash", Now.AddHours(2), TokenLifetime);

        Assert.Throws<DomainException>(() => user.ConfirmEmailChange("wrong", Now.AddHours(3)));
        Assert.Equal("user@example.com", user.Email.Value);
    }

    [Fact]
    public void ConfirmEmailChange_WithoutPendingChange_Throws()
    {
        var user = RegisterUser(out _);
        user.ConfirmEmail("confirm-hash", Now.AddHours(1));

        Assert.Throws<DomainException>(() => user.ConfirmEmailChange("change-hash", Now.AddHours(3)));
    }
}
