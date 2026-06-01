namespace Ambiquality.Core.Domain.Vocabulary;

/// <summary>
/// Stable QUDT URIs for the IEQ quantities this platform tracks.
/// All URIs verified against the live QUDT ontology (qudt.org) on 2026-05-31.
/// </summary>
public static class QudtVocabulary
{
    private const string QuantityKindBase = "http://qudt.org/vocab/quantitykind/";
    private const string UnitBase         = "http://qudt.org/vocab/unit/";

    // Quantity kinds — verified bidirectional (unit.hasQuantityKind confirmed) unless noted
    public static readonly string QuantityKindAmountOfSubstanceFraction  = QuantityKindBase + "AmountOfSubstanceFraction";
    public static readonly string QuantityKindMassDensity                = QuantityKindBase + "MassDensity";
    public static readonly string QuantityKindTemperature                = QuantityKindBase + "Temperature";
    public static readonly string QuantityKindRelativeHumidity           = QuantityKindBase + "RelativeHumidity";
    public static readonly string QuantityKindSpeed                      = QuantityKindBase + "Speed";
    /// <summary>
    /// AtmosphericPressure lists unit:PA as applicableUnit; unit:PA declares hasQuantityKind:ForcePerArea
    /// (the generic dimensional kind). AtmosphericPressure is skos:broader Pressure — this is the
    /// standard QUDT pattern: units point to dimensional kinds, semantic kinds point to applicable units.
    /// </summary>
    public static readonly string QuantityKindAtmosphericPressure        = QuantityKindBase + "AtmosphericPressure";
    public static readonly string QuantityKindIlluminance                = QuantityKindBase + "Illuminance";
    /// <summary>Slug is CorrelatedColorTemperature — ColorTemperature does not exist in QUDT.</summary>
    public static readonly string QuantityKindCorrelatedColorTemperature = QuantityKindBase + "CorrelatedColorTemperature";
    public static readonly string QuantityKindSoundPressureLevel         = QuantityKindBase + "SoundPressureLevel";

    // Units
    public static readonly string UnitPpm         = UnitBase + "PPM";
    public static readonly string UnitPpb         = UnitBase + "PPB";
    public static readonly string UnitMicrogPerM3 = UnitBase + "MicroGM-PER-M3";
    public static readonly string UnitDegC        = UnitBase + "DEG_C";
    public static readonly string UnitPercentRh   = UnitBase + "PERCENT_RH";
    public static readonly string UnitMPerSec     = UnitBase + "M-PER-SEC";
    public static readonly string UnitPa          = UnitBase + "PA";
    public static readonly string UnitLux         = UnitBase + "LUX";
    public static readonly string UnitK           = UnitBase + "K";
    /// <summary>A-weighted sound pressure level (dB(A)).</summary>
    public static readonly string UnitDeciBA      = UnitBase + "DeciB_A";

    private static readonly IReadOnlyDictionary<string, (string QuantityKind, string Unit)> Map =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            // Gases
            ["co2"]         = (QuantityKindAmountOfSubstanceFraction,  UnitPpm),
            ["eco2"]        = (QuantityKindAmountOfSubstanceFraction,  UnitPpm),
            ["co"]          = (QuantityKindAmountOfSubstanceFraction,  UnitPpm),
            ["o3"]          = (QuantityKindMassDensity,                UnitMicrogPerM3),
            ["no2"]         = (QuantityKindMassDensity,                UnitMicrogPerM3),
            ["so2"]         = (QuantityKindMassDensity,                UnitMicrogPerM3),
            ["voc"]         = (QuantityKindAmountOfSubstanceFraction,  UnitPpb),

            // Particulate matter — all MassDensity; size fractions encoded in the code only
            ["pm1"]         = (QuantityKindMassDensity,                UnitMicrogPerM3),
            ["pm2_5"]       = (QuantityKindMassDensity,                UnitMicrogPerM3),
            ["pm4"]         = (QuantityKindMassDensity,                UnitMicrogPerM3),
            ["pm10"]        = (QuantityKindMassDensity,                UnitMicrogPerM3),

            // Thermal comfort
            ["temperature"] = (QuantityKindTemperature,                UnitDegC),
            ["humidity"]    = (QuantityKindRelativeHumidity,           UnitPercentRh),
            ["air_velocity"]= (QuantityKindSpeed,                      UnitMPerSec),
            ["pressure"]    = (QuantityKindAtmosphericPressure,        UnitPa),

            // Light
            ["illuminance"] = (QuantityKindIlluminance,                UnitLux),
            ["cct"]         = (QuantityKindCorrelatedColorTemperature, UnitK),

            // Acoustics
            ["laeq"]        = (QuantityKindSoundPressureLevel,         UnitDeciBA),
        };

    /// <summary>
    /// Returns the QUDT quantity kind and unit URIs for the given parameter code,
    /// or <c>null</c> if the code is not in the vocabulary.
    /// </summary>
    public static (string QuantityKindUri, string UnitUri)? TryResolve(string parameterCode)
    {
        if (Map.TryGetValue(parameterCode, out var entry))
            return entry;
        return null;
    }
}
