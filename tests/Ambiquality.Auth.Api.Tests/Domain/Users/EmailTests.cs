using Xunit;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Tests.Domain.Users;

public class EmailTests
{
    [Fact]
    public void Create_WithValidEmail_ReturnsEmail()
    {
        var email = Email.Create("test@example.com");
        Assert.Equal("test@example.com", email.Value);
    }

    [Fact]
    public void Create_NormalizesToLowercase()
    {
        var email = Email.Create("Test@Example.COM");
        Assert.Equal("test@example.com", email.Value);
    }

    [Fact]
    public void Create_WithEmptyString_ThrowsDomainException()
    {
        Assert.Throws<InvalidEmailException>(() => Email.Create(""));
    }

    [Fact]
    public void Create_WithWhitespace_ThrowsDomainException()
    {
        Assert.Throws<InvalidEmailException>(() => Email.Create("   "));
    }

    [Fact]
    public void Create_WithoutAtSign_ThrowsDomainException()
    {
        Assert.Throws<InvalidEmailException>(() => Email.Create("invalid.email"));
    }

    [Fact]
    public void Create_WithNoLocalPart_ThrowsDomainException()
    {
        Assert.Throws<InvalidEmailException>(() => Email.Create("@example.com"));
    }

    [Fact]
    public void Create_WithNoDomain_ThrowsDomainException()
    {
        Assert.Throws<InvalidEmailException>(() => Email.Create("user@"));
    }

    [Fact]
    public void Create_WithNoDomainExtension_ThrowsDomainException()
    {
        Assert.Throws<InvalidEmailException>(() => Email.Create("user@localhost"));
    }

    [Fact]
    public void TwoEmailsWithSameValue_AreEqual()
    {
        var email1 = Email.Create("test@example.com");
        var email2 = Email.Create("test@example.com");
        Assert.Equal(email1, email2);
    }

    [Fact]
    public void TwoEmailsWithDifferentValue_AreNotEqual()
    {
        var email1 = Email.Create("test@example.com");
        var email2 = Email.Create("other@example.com");
        Assert.NotEqual(email1, email2);
    }

    [Fact]
    public void EmailEqualityIsCaseInsensitive()
    {
        var email1 = Email.Create("Test@Example.COM");
        var email2 = Email.Create("test@example.com");
        Assert.Equal(email1, email2);
    }
}
