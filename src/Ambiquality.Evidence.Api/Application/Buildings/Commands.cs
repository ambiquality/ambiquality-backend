namespace Ambiquality.Evidence.Api.Application.Buildings;

/// <summary>Inputs for the Building aggregate use cases (UC05, UC07).</summary>
public sealed record RegisterBuildingCommand(
    string Name,
    long AddressPointCode,
    string? StreetName,
    int HouseNumber,
    string HouseNumberType,
    int? OrientationNumber,
    string? OrientationNumberLetter,
    string MunicipalityName,
    string? MunicipalityPartName,
    string Psc,
    string? DistrictName,
    string? RegionName,
    string BuildingTypeCode,
    double? Latitude,
    double? Longitude,
    short? YearBuilt,
    short? YearRenovated,
    long? StreetCode = null,
    long? MunicipalityCode = null,
    long? MunicipalityPartCode = null,
    long? DistrictCode = null,
    long? RegionCode = null);

public sealed record RegisterBuildingResult(Guid Id, string UriSlug);

public sealed record ChangeBuildingNameCommand(Guid BuildingId, string NewName, DateTime ValidFrom);

public sealed record ChangeBuildingAddressCommand(
    Guid BuildingId,
    long AddressPointCode,
    string? StreetName,
    int HouseNumber,
    string HouseNumberType,
    int? OrientationNumber,
    string? OrientationNumberLetter,
    string MunicipalityName,
    string? MunicipalityPartName,
    string Psc,
    string? DistrictName,
    string? RegionName,
    DateTime ValidFrom,
    long? StreetCode = null,
    long? MunicipalityCode = null,
    long? MunicipalityPartCode = null,
    long? DistrictCode = null,
    long? RegionCode = null);

public sealed record ChangeBuildingTypeCommand(Guid BuildingId, string NewTypeCode, DateTime ValidFrom);

public sealed record ChangeBuildingLocationCommand(
    Guid BuildingId, double? Latitude, double? Longitude, DateTime ValidFrom);

public sealed record ChangeBuildingYearsCommand(
    Guid BuildingId, short? YearBuilt, short? YearRenovated, DateTime ValidFrom);
