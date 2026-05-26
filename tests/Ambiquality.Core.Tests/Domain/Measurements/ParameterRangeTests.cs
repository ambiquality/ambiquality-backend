using Ambiquality.Core.Domain.Measurements;

namespace Ambiquality.Core.Tests.Domain.Measurements;

public sealed class ParameterRangeTests
{
    [Theory]
    [InlineData(0, true)]      // lower bound inclusive
    [InlineData(50_000, true)] // upper bound inclusive
    [InlineData(412, true)]
    [InlineData(-1, false)]
    [InlineData(50_001, false)]
    public void Contains_is_inclusive_on_both_bounds(double value, bool expected)
    {
        var range = new ParameterRange("co2", 0, 50_000, "ppm");
        Assert.Equal(expected, range.Contains(value));
    }

    [Fact]
    public void Constructor_rejects_inverted_bounds()
    {
        Assert.Throws<ArgumentException>(() => new ParameterRange("co2", 100, 50, "ppm"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_rejects_blank_code(string code)
    {
        Assert.Throws<ArgumentException>(() => new ParameterRange(code, 0, 1, null));
    }
}
