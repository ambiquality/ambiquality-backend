using Ambiquality.Core.Domain.Rooms;

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
}
