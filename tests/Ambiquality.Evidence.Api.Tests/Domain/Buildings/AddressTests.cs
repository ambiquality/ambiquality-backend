using Ambiquality.Evidence.Api.Domain.Buildings;

namespace Ambiquality.Evidence.Api.Tests.Domain.Buildings;

public class AddressTests
{
    private static Address Sample(
        long addressPointCode = 21794547,
        string? streetName = "Náměstí Winstona Churchilla",
        int houseNumber = 1938,
        string houseNumberType = "č.p.",
        int? orientationNumber = 4,
        string? orientationNumberLetter = null,
        string municipalityName = "Praha",
        string? municipalityPartName = "Žižkov",
        string psc = "13067",
        string? districtName = "Hlavní město Praha",
        string? regionName = "Hlavní město Praha",
        long? streetCode = 650421,
        long? municipalityCode = 554782,
        long? municipalityPartCode = 400581,
        long? districtCode = 1100,
        long? regionCode = 19) =>
        Address.Create(addressPointCode, streetName, houseNumber, houseNumberType, orientationNumber,
            orientationNumberLetter, municipalityName, municipalityPartName, psc, districtName, regionName,
            streetCode, municipalityCode, municipalityPartCode, districtCode, regionCode);

    [Fact]
    public void Create_WithValidFields_ReturnsAddress()
    {
        var address = Sample();

        Assert.Equal(21794547, address.AddressPointCode);
        Assert.Equal("Náměstí Winstona Churchilla", address.StreetName);
        Assert.Equal(1938, address.HouseNumber);
        Assert.Equal("č.p.", address.HouseNumberType);
        Assert.Equal(4, address.OrientationNumber);
        Assert.Equal("Praha", address.MunicipalityName);
        Assert.Equal("Žižkov", address.MunicipalityPartName);
        Assert.Equal("13067", address.Psc);
    }

    [Fact]
    public void AddressPointIri_BuildsRuianIri()
    {
        Assert.Equal(
            "https://linked.cuzk.cz/resource/ruian/adresni-misto/21794547",
            Sample().AddressPointIri);
    }

    [Fact]
    public void TerritorialIris_BuildRuianIrisFromCodes()
    {
        var address = Sample();
        Assert.Equal("https://linked.cuzk.cz/resource/ruian/ulice/650421", address.StreetIri);
        Assert.Equal("https://linked.cuzk.cz/resource/ruian/obec/554782", address.MunicipalityIri);
        Assert.Equal("https://linked.cuzk.cz/resource/ruian/cast-obce/400581", address.MunicipalityPartIri);
        Assert.Equal("https://linked.cuzk.cz/resource/ruian/okres/1100", address.DistrictIri);
        Assert.Equal("https://linked.cuzk.cz/resource/ruian/vusc/19", address.RegionIri);
    }

    [Fact]
    public void TerritorialIris_AreNullWhenCodesAbsent()
    {
        var address = Sample(streetCode: null, municipalityCode: null,
            municipalityPartCode: null, districtCode: null, regionCode: null);
        Assert.Null(address.StreetIri);
        Assert.Null(address.MunicipalityIri);
        Assert.Null(address.MunicipalityPartIri);
        Assert.Null(address.DistrictIri);
        Assert.Null(address.RegionIri);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public void Create_WithNonPositiveMunicipalityCode_Throws(long code)
    {
        Assert.Throws<ArgumentException>(() => Sample(municipalityCode: code));
    }

    [Fact]
    public void Create_WithoutStreet_IsAllowed()
    {
        // Small municipalities have no street names — the house number identifies the address.
        var address = Sample(streetName: null, municipalityPartName: null);
        Assert.Null(address.StreetName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithNonPositiveAddressPointCode_Throws(long code)
    {
        Assert.Throws<ArgumentException>(() => Sample(addressPointCode: code));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveHouseNumber_Throws(int houseNumber)
    {
        Assert.Throws<ArgumentException>(() => Sample(houseNumber: houseNumber));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bogus")]
    [InlineData("č.x.")]
    public void Create_WithInvalidHouseNumberType_Throws(string type)
    {
        Assert.Throws<ArgumentException>(() => Sample(houseNumberType: type));
    }

    [Fact]
    public void Create_WithEmptyMunicipality_Throws()
    {
        Assert.Throws<ArgumentException>(() => Sample(municipalityName: ""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1306")]
    [InlineData("130677")]
    [InlineData("abcde")]
    public void Create_WithInvalidPsc_Throws(string psc)
    {
        Assert.Throws<ArgumentException>(() => Sample(psc: psc));
    }

    [Fact]
    public void Create_NormalisesPscByStrippingSpaces()
    {
        var address = Sample(psc: "130 67");
        Assert.Equal("13067", address.Psc);
    }

    [Fact]
    public void ToText_ComposesCzechPostalLine()
    {
        Assert.Equal(
            "Náměstí Winstona Churchilla 1938/4, 130 67 Praha",
            Sample().ToText());
    }

    [Fact]
    public void ToText_WithoutStreet_UsesMunicipalPart()
    {
        var address = Sample(streetName: null, orientationNumber: null);
        Assert.Equal("Žižkov 1938, 130 67 Praha", address.ToText());
    }

    [Fact]
    public void TwoAddressesWithSameValues_AreEqual()
    {
        Assert.Equal(Sample(), Sample());
    }
}
