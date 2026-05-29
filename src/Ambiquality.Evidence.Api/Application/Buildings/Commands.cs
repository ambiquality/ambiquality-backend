namespace Ambiquality.Evidence.Api.Application.Buildings;

/// <summary>Inputs for the Building aggregate use cases (UC05, UC07).</summary>
public sealed record RegisterBuildingCommand(
    string Name,
    string Street,
    string City,
    string Postcode,
    string Country,
    string BuildingTypeCode,
    double? Latitude,
    double? Longitude,
    string AnonymizationLevel,
    short? YearBuilt,
    short? YearRenovated);

public sealed record RegisterBuildingResult(Guid Id, string UriSlug);

public sealed record ChangeBuildingNameCommand(Guid BuildingId, string NewName, DateTime ValidFrom);

public sealed record ChangeBuildingAddressCommand(
    Guid BuildingId, string Street, string City, string Postcode, string Country, DateTime ValidFrom);

public sealed record ChangeBuildingTypeCommand(Guid BuildingId, string NewTypeCode, DateTime ValidFrom);

public sealed record ChangeBuildingLocationCommand(
    Guid BuildingId, double? Latitude, double? Longitude, string AnonymizationLevel, DateTime ValidFrom);

public sealed record ChangeBuildingYearsCommand(
    Guid BuildingId, short? YearBuilt, short? YearRenovated, DateTime ValidFrom);
