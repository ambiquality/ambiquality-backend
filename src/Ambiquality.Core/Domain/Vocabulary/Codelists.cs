using Ambiquality.Core.Domain.Rooms;

namespace Ambiquality.Core.Domain.Vocabulary;

/// <summary>
/// One concept in a controlled codelist: its <see cref="Code"/> (the stored notation)
/// and parallel English / Czech labels. The labels back the SKOS <c>skos:prefLabel</c>s
/// the Public API publishes; the code backs write-time validation in Evidence.Api.
/// </summary>
public sealed record CodelistConcept(string Code, string LabelEn, string LabelCs);

/// <summary>
/// A controlled vocabulary (a SKOS <c>skos:ConceptScheme</c> / Czech <em>číselník</em>):
/// a named, closed set of <see cref="CodelistConcept"/>s. Membership is case-insensitive.
/// </summary>
public sealed class Codelist
{
    private readonly IReadOnlyDictionary<string, CodelistConcept> _byCode;

    public Codelist(string scheme, params CodelistConcept[] concepts)
    {
        Scheme = scheme;
        Concepts = concepts;
        _byCode = concepts.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The scheme slug, e.g. <c>building-type</c> — used in the concept IRIs.</summary>
    public string Scheme { get; }

    /// <summary>The concepts, in definition order.</summary>
    public IReadOnlyList<CodelistConcept> Concepts { get; }

    /// <summary>True when the code is part of this codelist (case-insensitive).</summary>
    public bool IsValid(string code) => _byCode.ContainsKey(code);

    /// <summary>The concept for a code, or null if the code is not in the codelist.</summary>
    public CodelistConcept? TryGet(string code) =>
        _byCode.TryGetValue(code, out var concept) ? concept : null;
}

/// <summary>
/// The platform's controlled catalog codelists, shared by Evidence.Api (write validation)
/// and Public.Api (SKOS publication). Codes are the authoritative stored notations; the
/// labels are the published bilingual concept labels.
/// </summary>
public static class Codelists
{
    /// <summary>Building typology (<c>evidence.building_type</c>).</summary>
    public static readonly Codelist BuildingType = new("building-type",
        new CodelistConcept("residential", "Residential building", "Bytový dům"),
        new CodelistConcept("family_house", "Family house", "Rodinný dům"),
        new CodelistConcept("office", "Office building", "Administrativní budova"),
        new CodelistConcept("educational", "Educational building", "Školská budova"),
        new CodelistConcept("healthcare", "Healthcare facility", "Zdravotnické zařízení"),
        new CodelistConcept("retail", "Retail building", "Obchodní budova"),
        new CodelistConcept("industrial", "Industrial building", "Průmyslová budova"),
        new CodelistConcept("other", "Other", "Jiné"));

    /// <summary>Room function / use.</summary>
    public static readonly Codelist RoomFunction = new("room-function",
        new CodelistConcept("office", "Office", "Kancelář"),
        new CodelistConcept("classroom", "Classroom", "Učebna"),
        new CodelistConcept("conference", "Conference room", "Konferenční místnost"),
        new CodelistConcept("lab", "Laboratory", "Laboratoř"),
        new CodelistConcept("kitchen", "Kitchen", "Kuchyně"),
        new CodelistConcept("storage", "Storage", "Sklad"),
        new CodelistConcept("corridor", "Corridor", "Chodba"),
        new CodelistConcept("other", "Other", "Jiné"));

    /// <summary>Room ventilation strategy.</summary>
    public static readonly Codelist VentilationType = new("ventilation-type",
        new CodelistConcept("natural", "Natural ventilation", "Přirozené větrání"),
        new CodelistConcept("mechanical", "Mechanical ventilation", "Nucené větrání"),
        new CodelistConcept("hybrid", "Hybrid (mixed-mode) ventilation", "Hybridní větrání"),
        new CodelistConcept("none", "No ventilation", "Bez větrání"));

    /// <summary>Indoor/outdoor pollution sources affecting a room.</summary>
    public static readonly Codelist PollutionSource = new("pollution-source",
        new CodelistConcept("traffic", "Road traffic", "Doprava"),
        new CodelistConcept("industry", "Industry", "Průmysl"),
        new CodelistConcept("construction", "Construction", "Stavební činnost"),
        new CodelistConcept("cooking", "Cooking", "Vaření"),
        new CodelistConcept("chemicals", "Chemical products", "Chemické látky"),
        new CodelistConcept("tobacco_smoke", "Tobacco smoke", "Tabákový kouř"),
        new CodelistConcept("none", "None", "Žádné"),
        new CodelistConcept("other", "Other", "Jiné"));

    /// <summary>Room occupancy duration (codes from <see cref="ExposureCode"/>).</summary>
    public static readonly Codelist Exposure = new("exposure",
        new CodelistConcept(ExposureCode.Short, "Short-term occupancy", "Krátkodobý pobyt"),
        new CodelistConcept(ExposureCode.Medium, "Medium-term occupancy", "Střednědobý pobyt"),
        new CodelistConcept(ExposureCode.Long, "Long-term occupancy", "Dlouhodobý pobyt"));

    /// <summary>Sensor operational lifecycle. Codes mirror Evidence.Api's <c>SensorStatus</c>,
    /// which lives in the Evidence service and is not visible to Public.Api.</summary>
    public static readonly Codelist SensorStatus = new("sensor-status",
        new CodelistConcept("active", "Active", "Aktivní"),
        new CodelistConcept("maintenance", "Under maintenance", "V údržbě"),
        new CodelistConcept("decommissioned", "Decommissioned", "Vyřazeno"));

    /// <summary>All published codelists, in catalogue order.</summary>
    public static readonly IReadOnlyList<Codelist> All =
        [BuildingType, RoomFunction, VentilationType, PollutionSource, Exposure, SensorStatus];

    /// <summary>The codelist for a scheme slug (e.g. <c>building-type</c>), or null if unknown.</summary>
    public static Codelist? ByScheme(string scheme) =>
        All.FirstOrDefault(c => string.Equals(c.Scheme, scheme, StringComparison.OrdinalIgnoreCase));
}
