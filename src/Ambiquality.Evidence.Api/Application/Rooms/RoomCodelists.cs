using Ambiquality.Core.Domain.Rooms;
using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Evidence.Api.Application.Rooms;

/// <summary>
/// Validates user-supplied room codelist codes on write, translating an unknown
/// code into the <see cref="UnknownCodelistCodeException"/> the API maps to a 400.
/// Mirrors <c>SensorCodelists</c>.
/// </summary>
internal static class RoomCodelists
{
    /// <summary>
    /// Ensures an optional exposure code is part of the <see cref="ExposureCode"/>
    /// codelist. A <c>null</c> code clears the attribute and is always allowed.
    /// </summary>
    public static void ValidateExposure(string? exposureCode)
    {
        if (exposureCode is not null && !ExposureCode.IsValid(exposureCode))
            throw new UnknownCodelistCodeException("exposure", exposureCode);
    }

    /// <summary>Ensures an optional function code is in the codelist; <c>null</c> clears it.</summary>
    public static void ValidateFunction(string? functionCode) =>
        Require(Codelists.RoomFunction, "room_function", functionCode);

    /// <summary>Ensures an optional ventilation code is in the codelist; <c>null</c> clears it.</summary>
    public static void ValidateVentilation(string? ventilationType) =>
        Require(Codelists.VentilationType, "ventilation_type", ventilationType);

    /// <summary>Ensures a pollution-source code is in the codelist (the code is required).</summary>
    public static void ValidatePollutionSource(string sourceCode) =>
        Require(Codelists.PollutionSource, "pollution_source", sourceCode);

    private static void Require(Codelist codelist, string name, string? code)
    {
        if (code is not null && !codelist.IsValid(code))
            throw new UnknownCodelistCodeException(name, code);
    }
}
