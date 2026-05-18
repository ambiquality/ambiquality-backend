using Ambiquality.Evidence.Api.Domain.Buildings;

namespace Ambiquality.Evidence.Api.Tests.Domain.Buildings;

public class CoordinatesTests
{
    [Fact]
    public void Create_WithValidLatLon_ReturnsCoordinates()
    {
        var coords = Coordinates.Create(50.087, 14.421);
        Assert.Equal(50.087, coords.Latitude);
        Assert.Equal(14.421, coords.Longitude);
    }

    [Theory]
    [InlineData(-90.0, 0.0)]
    [InlineData(90.0, 0.0)]
    [InlineData(0.0, -180.0)]
    [InlineData(0.0, 180.0)]
    public void Create_AtBoundaryValues_Succeeds(double lat, double lon)
    {
        var coords = Coordinates.Create(lat, lon);
        Assert.Equal(lat, coords.Latitude);
        Assert.Equal(lon, coords.Longitude);
    }

    [Theory]
    [InlineData(-90.001, 0.0)]
    [InlineData(90.001, 0.0)]
    [InlineData(0.0, -180.001)]
    [InlineData(0.0, 180.001)]
    [InlineData(double.NaN, 0.0)]
    [InlineData(0.0, double.NaN)]
    public void Create_OutOfRange_Throws(double lat, double lon)
    {
        Assert.Throws<ArgumentException>(() => Coordinates.Create(lat, lon));
    }

    [Fact]
    public void TwoCoordinatesWithSameValues_AreEqual()
    {
        var a = Coordinates.Create(50.0, 14.0);
        var b = Coordinates.Create(50.0, 14.0);
        Assert.Equal(a, b);
    }
}
