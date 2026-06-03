using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Evidence.Api.Application.Buildings;

/// <summary>
/// Validates user-supplied building codelist codes on write, translating an unknown
/// code into the <see cref="UnknownCodelistCodeException"/> the API maps to a 400.
/// Mirrors <c>RoomCodelists</c> / <c>SensorCodelists</c>.
/// </summary>
internal static class BuildingCodelists
{
    /// <summary>Ensures an optional building-type code is in the codelist; <c>null</c> clears it.</summary>
    public static void ValidateType(string? buildingTypeCode)
    {
        if (buildingTypeCode is not null && !Codelists.BuildingType.IsValid(buildingTypeCode))
            throw new UnknownCodelistCodeException("building_type", buildingTypeCode);
    }
}
