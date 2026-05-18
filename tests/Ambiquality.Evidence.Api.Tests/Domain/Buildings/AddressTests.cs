using Ambiquality.Evidence.Api.Domain.Buildings;

namespace Ambiquality.Evidence.Api.Tests.Domain.Buildings;

public class AddressTests
{
    [Fact]
    public void Create_WithValidFields_ReturnsAddress()
    {
        var address = Address.Create("Náměstí 1", "Praha", "11000", "CZ");

        Assert.Equal("Náměstí 1", address.Street);
        Assert.Equal("Praha", address.City);
        Assert.Equal("11000", address.Postcode);
        Assert.Equal("CZ", address.Country);
    }

    [Theory]
    [InlineData(null, "Praha", "11000", "CZ")]
    [InlineData("", "Praha", "11000", "CZ")]
    [InlineData("Street", null, "11000", "CZ")]
    [InlineData("Street", "", "11000", "CZ")]
    [InlineData("Street", "Praha", null, "CZ")]
    [InlineData("Street", "Praha", "", "CZ")]
    [InlineData("Street", "Praha", "11000", null)]
    [InlineData("Street", "Praha", "11000", "")]
    public void Create_WithMissingField_Throws(string? street, string? city, string? postcode, string? country)
    {
        Assert.Throws<ArgumentException>(() => Address.Create(street!, city!, postcode!, country!));
    }

    [Fact]
    public void Create_NormalisesCountryToUpperInvariant()
    {
        var address = Address.Create("S", "P", "11000", "cz");
        Assert.Equal("CZ", address.Country);
    }

    [Fact]
    public void TwoAddressesWithSameValues_AreEqual()
    {
        var a = Address.Create("S", "P", "11000", "CZ");
        var b = Address.Create("S", "P", "11000", "CZ");
        Assert.Equal(a, b);
    }
}
