namespace Ambiquality.Evidence.Api.Domain.Common;

/// <summary>
/// Stable QUDT URIs for the IEQ quantities this platform tracks.
/// All URIs verified against the live QUDT ontology (qudt.org) on 2026-05-31.
/// </summary>
public static class QudtVocabulary
{
    private const string QuantityKindBase = "http://qudt.org/vocab/quantitykind/";
    private const string UnitBase = "http://qudt.org/vocab/unit/";

    public static readonly string QuantityKindAmountOfSubstanceFraction = QuantityKindBase + "AmountOfSubstanceFraction";
    public static readonly string QuantityKindTemperature               = QuantityKindBase + "Temperature";
    public static readonly string QuantityKindRelativeHumidity          = QuantityKindBase + "RelativeHumidity";
    public static readonly string QuantityKindMassDensity               = QuantityKindBase + "MassDensity";
    public static readonly string QuantityKindSoundPressureLevel        = QuantityKindBase + "SoundPressureLevel";
    public static readonly string QuantityKindIlluminance               = QuantityKindBase + "Illuminance";

    public static readonly string UnitPpm         = UnitBase + "PPM";
    public static readonly string UnitDegC        = UnitBase + "DEG_C";
    public static readonly string UnitPercentRh   = UnitBase + "PERCENT_RH";
    public static readonly string UnitMicrogPerM3 = UnitBase + "MicroGM-PER-M3";
    public static readonly string UnitPpb         = UnitBase + "PPB";
    public static readonly string UnitDeciB       = UnitBase + "DeciB";
    public static readonly string UnitLux         = UnitBase + "LUX";

    private static readonly IReadOnlyDictionary<string, (string QuantityKind, string Unit)> Map =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["co2"]         = (QuantityKindAmountOfSubstanceFraction, UnitPpm),
            ["temperature"] = (QuantityKindTemperature,               UnitDegC),
            ["humidity"]    = (QuantityKindRelativeHumidity,          UnitPercentRh),
            ["pm"]          = (QuantityKindMassDensity,               UnitMicrogPerM3),
            ["voc"]         = (QuantityKindAmountOfSubstanceFraction, UnitPpb),
            ["acoustics"]   = (QuantityKindSoundPressureLevel,        UnitDeciB),
            ["light"]       = (QuantityKindIlluminance,               UnitLux),
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
