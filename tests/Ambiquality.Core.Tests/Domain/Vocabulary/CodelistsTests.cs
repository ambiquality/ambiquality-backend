using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Core.Tests.Domain.Vocabulary;

// Shares the process-wide Codelists static singletons with VocabularyExtensionsLoaderTests,
// which mutates them via Codelist.Add. Same collection ⇒ the two classes never run in
// parallel, so an Add can't modify a Concepts list mid-enumeration here.
[Collection(CodelistGlobalState.Name)]
public sealed class CodelistsTests
{
    public static IEnumerable<object[]> AllCodelists =>
        Codelists.All.Select(c => new object[] { c });

    [Fact]
    public void All_ContainsEverySixCodelists_AndByScheme_ResolvesThem()
    {
        Assert.Equal(6, Codelists.All.Count);
        Assert.All(Codelists.All, c => Assert.Same(c, Codelists.ByScheme(c.Scheme)));
        Assert.Null(Codelists.ByScheme("no-such-scheme"));
    }

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
