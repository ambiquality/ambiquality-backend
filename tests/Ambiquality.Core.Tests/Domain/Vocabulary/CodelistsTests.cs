using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Core.Tests.Domain.Vocabulary;

public sealed class CodelistsTests
{
    public static IEnumerable<object[]> AllCodelists =>
    [
        [Codelists.BuildingType],
        [Codelists.RoomFunction],
        [Codelists.VentilationType],
        [Codelists.PollutionSource]
    ];

    [Fact]
    public void IsValid_IsCaseInsensitive()
    {
        Assert.True(Codelists.BuildingType.IsValid("office"));
        Assert.True(Codelists.BuildingType.IsValid("OFFICE"));
        Assert.False(Codelists.BuildingType.IsValid("castle"));
    }

    [Fact]
    public void TryGet_ReturnsConcept_OrNull()
    {
        var concept = Codelists.VentilationType.TryGet("mechanical");
        Assert.NotNull(concept);
        Assert.Equal("Mechanical ventilation", concept.LabelEn);
        Assert.Equal("Nucené větrání", concept.LabelCs);

        Assert.Null(Codelists.VentilationType.TryGet("passive"));
    }

    [Theory]
    [MemberData(nameof(AllCodelists))]
    public void EveryConcept_HasSchemeAndBilingualLabels(Codelist codelist)
    {
        Assert.False(string.IsNullOrWhiteSpace(codelist.Scheme));
        Assert.NotEmpty(codelist.Concepts);
        Assert.All(codelist.Concepts, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Code));
            Assert.False(string.IsNullOrWhiteSpace(c.LabelEn));
            Assert.False(string.IsNullOrWhiteSpace(c.LabelCs));
        });
    }

    [Theory]
    [MemberData(nameof(AllCodelists))]
    public void Codes_AreUnique(Codelist codelist)
    {
        var codes = codelist.Concepts.Select(c => c.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
