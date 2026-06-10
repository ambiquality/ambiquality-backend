using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ambiquality.Core.Domain.Vocabulary;

/// <summary>One operator-defined codelist concept (POD-04).</summary>
public sealed record CodelistExtension(string Code, string LabelEn, string LabelCs);

/// <summary>
/// One operator-defined observable property / quantity (POD-04): the parameter code, an
/// English label, the canonical unit string ingestion validates against, the permitted
/// value range, and — optionally — its QUDT quantity-kind and unit URIs for linked-data
/// publication (both or neither).
/// </summary>
public sealed record PropertyExtension(
    string Code,
    string Label,
    string Unit,
    double MinValue,
    double MaxValue,
    string? QuantityKindUri,
    string? UnitUri);

/// <summary>The parsed vocabulary-extensions file: codelist concepts keyed by scheme slug, plus properties.</summary>
public sealed record VocabularyExtensionsDocument(
    IReadOnlyDictionary<string, IReadOnlyList<CodelistExtension>>? Codelists,
    IReadOnlyList<PropertyExtension>? Properties);

/// <summary>
/// POD-04 — vocabularies and codelists are extensible without touching source code.
/// At startup every service loads the operator's JSON extensions file (the
/// <c>Vocabulary:ExtensionsPath</c> configuration key / <c>Vocabulary__ExtensionsPath</c>
/// env var; see <c>conf/vocabulary-extensions.json</c>) and applies it <em>additively</em>
/// to the in-memory registries: <see cref="Codelists"/>,
/// <see cref="ObservablePropertyVocabulary"/> and <see cref="QudtVocabulary"/>.
/// A code that collides with a built-in is rejected, so already-published data can never
/// be reinterpreted — extension is strictly backward compatible. Ingestion.Api
/// additionally seeds an <c>ieq.parameter_ranges</c> row per extension property, and
/// Evidence.Api registers each property as a declarable <c>MeasuredParameter</c>.
/// </summary>
public static class VocabularyExtensionsLoader
{
    /// <summary>Configuration key holding the extensions-file path; unset/empty = no extensions.</summary>
    public const string PathConfigKey = "Vocabulary:ExtensionsPath";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// Loads and applies the extensions file at <paramref name="path"/>. A null/empty path
    /// is a no-op (returns null). A configured-but-missing file throws — a path that points
    /// nowhere is a deployment error, not a default to silently fall back from.
    /// </summary>
    public static VocabularyExtensionsDocument? LoadAndApply(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!File.Exists(path))
            throw new InvalidOperationException(
                $"Vocabulary extensions file '{path}' (from {PathConfigKey}) does not exist.");

        var document = Parse(File.ReadAllText(path));
        Apply(document);
        return document;
    }

    /// <summary>Parses the extensions JSON and validates its shape.</summary>
    public static VocabularyExtensionsDocument Parse(string json)
    {
        var document = JsonSerializer.Deserialize<VocabularyExtensionsDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException("Vocabulary extensions file is empty.");

        foreach (var (scheme, concepts) in document.Codelists ?? new Dictionary<string, IReadOnlyList<CodelistExtension>>())
        {
            if (Codelists.ByScheme(scheme) is null)
                throw new InvalidOperationException(
                    $"Vocabulary extensions reference unknown codelist scheme '{scheme}'. " +
                    $"Known schemes: {string.Join(", ", Codelists.All.Select(c => c.Scheme))}.");
            foreach (var concept in concepts)
                if (string.IsNullOrWhiteSpace(concept.Code))
                    throw new InvalidOperationException(
                        $"A '{scheme}' codelist extension is missing its 'code'.");
        }

        foreach (var property in document.Properties ?? [])
        {
            if (string.IsNullOrWhiteSpace(property.Code))
                throw new InvalidOperationException("A property extension is missing its 'code'.");
            if (string.IsNullOrWhiteSpace(property.Unit))
                throw new InvalidOperationException(
                    $"Property extension '{property.Code}' is missing its canonical 'unit'.");
            if (property.MinValue >= property.MaxValue)
                throw new InvalidOperationException(
                    $"Property extension '{property.Code}' needs minValue < maxValue.");
            if (property.QuantityKindUri is null != property.UnitUri is null)
                throw new InvalidOperationException(
                    $"Property extension '{property.Code}' must set quantityKindUri and unitUri together (or neither).");
        }

        return document;
    }

    /// <summary>
    /// Applies the document additively to the static registries. Idempotent: codes already
    /// present (built-in or previously applied) are skipped, never overwritten.
    /// </summary>
    public static void Apply(VocabularyExtensionsDocument document)
    {
        foreach (var (scheme, concepts) in document.Codelists ?? new Dictionary<string, IReadOnlyList<CodelistExtension>>())
        {
            var codelist = Codelists.ByScheme(scheme)!;
            foreach (var concept in concepts)
                codelist.Add(new CodelistConcept(concept.Code, concept.LabelEn, concept.LabelCs));
        }

        foreach (var property in document.Properties ?? [])
        {
            ObservablePropertyVocabulary.Add(
                new ObservablePropertyVocabulary.Entry(property.Code, property.Label, null));
            if (property.QuantityKindUri is not null && property.UnitUri is not null)
                QudtVocabulary.Add(property.Code, property.QuantityKindUri, property.UnitUri);
        }
    }
}
