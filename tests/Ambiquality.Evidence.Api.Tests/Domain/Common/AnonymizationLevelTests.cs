using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Tests.Domain.Common;

public class AnonymizationLevelTests
{
    [Fact]
    public void Precise_HasExpectedCode()
    {
        Assert.Equal("precise", AnonymizationLevel.Precise.Code);
    }

    [Fact]
    public void Street_HasExpectedCode()
    {
        Assert.Equal("street", AnonymizationLevel.Street.Code);
    }

    [Fact]
    public void Municipality_HasExpectedCode()
    {
        Assert.Equal("municipality", AnonymizationLevel.Municipality.Code);
    }

    [Theory]
    [InlineData("precise")]
    [InlineData("street")]
    [InlineData("municipality")]
    public void FromCode_WithKnownValue_ReturnsInstance(string code)
    {
        var level = AnonymizationLevel.FromCode(code);
        Assert.Equal(code, level.Code);
    }

    [Fact]
    public void FromCode_IsCaseInsensitive()
    {
        Assert.Equal(AnonymizationLevel.Precise, AnonymizationLevel.FromCode("PRECISE"));
    }

    [Fact]
    public void FromCode_WithUnknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => AnonymizationLevel.FromCode("city"));
    }

    [Fact]
    public void SameLevelInstances_AreEqual()
    {
        Assert.Equal(AnonymizationLevel.Precise, AnonymizationLevel.FromCode("precise"));
    }
}
