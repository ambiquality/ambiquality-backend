using Ambiquality.Auth.Api.Domain;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Tests.Domain.Users;

public class VerificationTokenTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Issue_CreatesUnconsumedToken()
    {
        var token = VerificationToken.Issue(
            "hash-abc", VerificationPurpose.EmailConfirmation, Now, TimeSpan.FromHours(24));

        Assert.Equal("hash-abc", token.TokenHash);
        Assert.Equal(VerificationPurpose.EmailConfirmation, token.Purpose);
        Assert.Equal(Now, token.CreatedAt);
        Assert.Equal(Now.AddHours(24), token.ExpiresAt);
        Assert.Null(token.ConsumedAt);
    }

    [Fact]
    public void Validate_WithMatchingHashAndPurpose_ReturnsTrue()
    {
        var token = VerificationToken.Issue(
            "hash-abc", VerificationPurpose.EmailConfirmation, Now, TimeSpan.FromHours(24));

        Assert.True(token.Validate("hash-abc", VerificationPurpose.EmailConfirmation, Now));
    }

    [Fact]
    public void Validate_WithWrongHash_ReturnsFalse()
    {
        var token = VerificationToken.Issue(
            "hash-abc", VerificationPurpose.EmailConfirmation, Now, TimeSpan.FromHours(24));

        Assert.False(token.Validate("wrong-hash", VerificationPurpose.EmailConfirmation, Now));
    }

    [Fact]
    public void Validate_WithWrongPurpose_ReturnsFalse()
    {
        var token = VerificationToken.Issue(
            "hash-abc", VerificationPurpose.EmailConfirmation, Now, TimeSpan.FromHours(24));

        Assert.False(token.Validate("hash-abc", VerificationPurpose.EmailChange, Now));
    }

    [Fact]
    public void Validate_WhenExpired_ReturnsFalse()
    {
        var token = VerificationToken.Issue(
            "hash-abc", VerificationPurpose.EmailConfirmation, Now, TimeSpan.FromHours(24));

        Assert.False(token.Validate("hash-abc", VerificationPurpose.EmailConfirmation, Now.AddHours(25)));
    }

    [Fact]
    public void Validate_WhenAlreadyConsumed_ReturnsFalse()
    {
        var token = VerificationToken.Issue(
            "hash-abc", VerificationPurpose.EmailConfirmation, Now, TimeSpan.FromHours(24));
        token.Consume(Now.AddHours(1));

        Assert.False(token.Validate("hash-abc", VerificationPurpose.EmailConfirmation, Now.AddHours(2)));
    }

    [Fact]
    public void Consume_MarksTokenConsumed()
    {
        var token = VerificationToken.Issue(
            "hash-abc", VerificationPurpose.EmailConfirmation, Now, TimeSpan.FromHours(24));

        token.Consume(Now.AddHours(1));

        Assert.Equal(Now.AddHours(1), token.ConsumedAt);
    }

    [Fact]
    public void Consume_WhenAlreadyConsumed_Throws()
    {
        var token = VerificationToken.Issue(
            "hash-abc", VerificationPurpose.EmailConfirmation, Now, TimeSpan.FromHours(24));
        token.Consume(Now.AddHours(1));

        Assert.Throws<DomainException>(() => token.Consume(Now.AddHours(2)));
    }
}
