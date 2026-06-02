namespace Ambiquality.Core.Domain.Vocabulary;

/// <summary>
/// The platform's observable properties — one specific, substance-distinguishing
/// concept per IEQ parameter code. These back <c>sosa:observedProperty</c> so a
/// consumer can tell PM2.5 from PM10, or VOC from CO₂.
/// </summary>
/// <remarks>
/// <see cref="QudtVocabulary"/> only carries the shared <em>dimensional</em>
/// quantity kind (<c>MassDensity</c>, <c>AmountOfSubstanceFraction</c>), which
/// collapses those distinctions — so it is the wrong thing to expose as the
/// observed property. This vocabulary mints a specific concept per parameter and,
/// where an authoritative external concept exists, links to it via
/// <c>skos:exactMatch</c>; the QUDT quantity kind + unit are still resolved from
/// <see cref="QudtVocabulary"/> and belong on <c>qudt:hasQuantityKind</c> /
/// <c>qudt:applicableUnit</c>.
/// </remarks>
public static class ObservablePropertyVocabulary
{
    /// <summary>
    /// An authoritative external concept a property is the same as (or close to).
    /// <see cref="IsExact"/> selects <c>skos:exactMatch</c> vs <c>skos:closeMatch</c>.
    /// </summary>
    public readonly record struct ExternalMatch(string Iri, bool IsExact);

    /// <summary>
    /// A platform observable property: its parameter <see cref="Code"/>, a human
    /// label, an optional <see cref="ExternalMatch"/>, and — via
    /// <see cref="QudtVocabulary"/> — its QUDT quantity kind and unit.
    /// </summary>
    public sealed record Entry(string Code, string Label, ExternalMatch? ExternalMatch)
    {
        /// <summary>The QUDT quantity-kind and unit URIs for this parameter.</summary>
        public (string QuantityKindUri, string UnitUri)? Qudt => QudtVocabulary.TryResolve(Code);
    }

    private const string EeaPollutantBase = "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/";

    // EEA/EIONET air-quality pollutant ids verified live against
    // dd.eionet.europa.eu on 2026-06-02. Only the regulated ambient pollutants
    // get an exactMatch; sensor-specific quantities (TVOC, PM1/PM4, eCO₂) and
    // physical comfort quantities have no authoritative pollutant concept and
    // rely on the minted IRI + QUDT quantity kind alone.
    private static ExternalMatch EeaExact(string id) => new(EeaPollutantBase + id, IsExact: true);

    // Ordered to mirror MeasuredParameter / the parameter_ranges seed grouping.
    private static readonly IReadOnlyList<Entry> Entries = new[]
    {
        // Gases — concentration
        new Entry("co2",          "Carbon dioxide",                                      null),
        new Entry("eco2",         "Equivalent carbon dioxide (eCO₂)",                    null),
        new Entry("co",           "Carbon monoxide",                                     EeaExact("10")),
        new Entry("o3",           "Ozone",                                               EeaExact("7")),
        new Entry("no2",          "Nitrogen dioxide",                                    EeaExact("8")),
        new Entry("so2",          "Sulphur dioxide",                                     EeaExact("1")),
        new Entry("voc",          "Volatile organic compounds (TVOC)",                   null),

        // Particulate matter
        new Entry("pm1",          "Particulate matter < 1 µm (PM1)",                     null),
        new Entry("pm2_5",        "Particulate matter < 2.5 µm (PM2.5)",                 EeaExact("6001")),
        new Entry("pm4",          "Particulate matter < 4 µm (PM4)",                     null),
        new Entry("pm10",         "Particulate matter < 10 µm (PM10)",                   EeaExact("5")),

        // Thermal comfort
        new Entry("temperature",  "Air temperature",                                     null),
        new Entry("humidity",     "Relative humidity",                                   null),
        new Entry("air_velocity", "Air velocity",                                        null),
        new Entry("pressure",     "Atmospheric pressure",                                null),

        // Light
        new Entry("illuminance",  "Illuminance",                                         null),
        new Entry("cct",          "Correlated colour temperature",                       null),

        // Acoustics
        new Entry("laeq",         "A-weighted equivalent continuous sound level (LAeq)", null),
    };

    private static readonly IReadOnlyDictionary<string, Entry> ByCode =
        Entries.ToDictionary(e => e.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>All observable properties, in catalogue order.</summary>
    public static IReadOnlyList<Entry> All => Entries;

    /// <summary>The observable property for a parameter code, or <c>null</c> if unknown.</summary>
    public static Entry? TryGet(string code) =>
        code is not null && ByCode.TryGetValue(code, out var entry) ? entry : null;
}
