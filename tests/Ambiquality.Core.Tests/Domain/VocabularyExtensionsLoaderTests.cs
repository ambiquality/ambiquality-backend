using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Core.Tests.Domain;

/// <summary>
/// POD-04: vocabulary extensions are additive and strictly backward compatible.
/// The registries are process-wide statics, so every test uses codes unique to it.
/// </summary>
public class VocabularyExtensionsLoaderTests
{
    [Fact]
    public void Apply_AddsCodelistConcept_ValidatableAndPublished()
    {
        var document = VocabularyExtensionsLoader.Parse("""
            {
              "codelists": {
                "building-type": [
                  { "code": "test_sports_facility", "labelEn": "Sports facility", "labelCs": "Sportovní zařízení" }
                ]
              }
            }
            """);

        VocabularyExtensionsLoader.Apply(document);

        Assert.True(Codelists.BuildingType.IsValid("test_sports_facility"));
        var concept = Codelists.BuildingType.TryGet("test_sports_facility");
        Assert.Equal("Sportovní zařízení", concept!.LabelCs);
        // Published through the same instance the codelist endpoints serve.
        Assert.Contains(Codelists.ByScheme("building-type")!.Concepts, c => c.Code == "test_sports_facility");
    }

    [Fact]
    public void Apply_AddsProperty_WithQudtResolutionAndObservableEntry()
    {
        var document = VocabularyExtensionsLoader.Parse("""
            {
              "properties": [
                {
                  "code": "test_radon",
                  "label": "Radon activity concentration",
                  "unit": "Bq/m³",
                  "minValue": 0,
                  "maxValue": 100000,
                  "quantityKindUri": "http://qudt.org/vocab/quantitykind/ActivityConcentration",
                  "unitUri": "http://qudt.org/vocab/unit/BQ-PER-M3"
                }
              ]
            }
            """);

        VocabularyExtensionsLoader.Apply(document);

        var entry = ObservablePropertyVocabulary.TryGet("test_radon");
        Assert.NotNull(entry);
        Assert.Equal("Radon activity concentration", entry!.Label);
        var qudt = QudtVocabulary.TryResolve("test_radon");
        Assert.Equal("http://qudt.org/vocab/unit/BQ-PER-M3", qudt!.Value.UnitUri);
    }

    [Fact]
    public void Apply_NeverOverwritesBuiltIns()
    {
        var document = VocabularyExtensionsLoader.Parse("""
            {
              "codelists": {
                "building-type": [ { "code": "office", "labelEn": "HIJACKED", "labelCs": "HIJACKED" } ]
              },
              "properties": [
                {
                  "code": "co2", "label": "HIJACKED", "unit": "mg/m³",
                  "minValue": 0, "maxValue": 1,
                  "quantityKindUri": "urn:x", "unitUri": "urn:y"
                }
              ]
            }
            """);

        VocabularyExtensionsLoader.Apply(document);

        Assert.Equal("Office building", Codelists.BuildingType.TryGet("office")!.LabelEn);
        Assert.Equal("Carbon dioxide", ObservablePropertyVocabulary.TryGet("co2")!.Label);
        Assert.Equal(QudtVocabulary.UnitPpm, QudtVocabulary.TryResolve("co2")!.Value.UnitUri);
    }

    [Fact]
    public void Apply_IsIdempotent()
    {
        var document = VocabularyExtensionsLoader.Parse("""
            {
              "codelists": {
                "room-function": [ { "code": "test_gym", "labelEn": "Gym", "labelCs": "Tělocvična" } ]
              }
            }
            """);

        VocabularyExtensionsLoader.Apply(document);
        VocabularyExtensionsLoader.Apply(document);

        Assert.Single(Codelists.RoomFunction.Concepts, c => c.Code == "test_gym");
    }

    [Fact]
    public void Parse_UnknownScheme_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => VocabularyExtensionsLoader.Parse("""
            { "codelists": { "no-such-scheme": [ { "code": "x", "labelEn": "x", "labelCs": "x" } ] } }
            """));
        Assert.Contains("no-such-scheme", ex.Message);
    }

    [Fact]
    public void Parse_PropertyWithInvalidRangeOrHalfQudt_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => VocabularyExtensionsLoader.Parse("""
            { "properties": [ { "code": "x", "label": "x", "unit": "u", "minValue": 5, "maxValue": 5 } ] }
            """));
        Assert.Throws<InvalidOperationException>(() => VocabularyExtensionsLoader.Parse("""
            { "properties": [ { "code": "x", "label": "x", "unit": "u", "minValue": 0, "maxValue": 1, "quantityKindUri": "urn:only-one" } ] }
            """));
    }

    [Fact]
    public void LoadAndApply_NullOrEmptyPath_IsNoOp()
    {
        Assert.Null(VocabularyExtensionsLoader.LoadAndApply(null));
        Assert.Null(VocabularyExtensionsLoader.LoadAndApply(""));
    }

    [Fact]
    public void LoadAndApply_MissingFile_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => VocabularyExtensionsLoader.LoadAndApply("/nonexistent/vocabulary.json"));
    }

    [Fact]
    public void LoadAndApply_ReadsCommentedFile_LikeTheShippedTemplate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vocab-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            // operator notes are allowed
            {
              "codelists": {},
              "properties": []
            }
            """);
        try
        {
            var document = VocabularyExtensionsLoader.LoadAndApply(path);
            Assert.NotNull(document);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
