using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Tests.Domain.Common;

public class UriSlugTests
{
    [Fact]
    public void Create_WithValidSlug_ReturnsSlug()
    {
        var slug = UriSlug.Create("my-building-01");
        Assert.Equal("my-building-01", slug.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespace_Throws(string value)
    {
        Assert.Throws<InvalidUriSlugException>(() => UriSlug.Create(value));
    }

    [Theory]
    [InlineData("UPPER")]
    [InlineData("with spaces")]
    [InlineData("with_underscore")]
    [InlineData("-leading-dash")]
    [InlineData("trailing-dash-")]
    [InlineData("dou--ble-dash-ok-but-startwith-dash-")]
    [InlineData("special!chars")]
    public void Create_WithInvalidCharacters_Throws(string value)
    {
        Assert.Throws<InvalidUriSlugException>(() => UriSlug.Create(value));
    }

    [Fact]
    public void Create_WithTooLong_Throws()
    {
        var tooLong = new string('a', 65);
        Assert.Throws<InvalidUriSlugException>(() => UriSlug.Create(tooLong));
    }

    [Fact]
    public void Create_AtMaxLength_Succeeds()
    {
        var maxLength = new string('a', 64);
        var slug = UriSlug.Create(maxLength);
        Assert.Equal(maxLength, slug.Value);
    }

    [Fact]
    public void TwoSlugsWithSameValue_AreEqual()
    {
        var a = UriSlug.Create("same-value");
        var b = UriSlug.Create("same-value");
        Assert.Equal(a, b);
    }
}
