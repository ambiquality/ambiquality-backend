using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Czech postal address of a building (current temporal state), following the OFN
/// "Adresy" (2020-07-01) standard. <see cref="AddressPointCode"/> is the RÚIAN
/// address-point anchor; the optional components are null when not recorded.
/// </summary>
public sealed record AddressDto(
    long? AddressPointCode,
    string? StreetName,
    int? HouseNumber,
    string? HouseNumberType,
    int? OrientationNumber,
    string? OrientationNumberLetter,
    string? MunicipalityName,
    string? MunicipalityPartName,
    string? Psc,
    string? DistrictName,
    string? RegionName);

/// <summary>
/// Public projection of a building. Coordinates are the precise stored values
/// (this is open data; there is no anonymization).
/// </summary>
public sealed record BuildingResponse(
    Guid Id,
    string Iri,
    string? Name,
    AddressDto Address,
    string? BuildingTypeCode,
    double? Latitude,
    double? Longitude,
    int? YearBuilt,
    int? YearRenovated,
    string License);

/// <summary>Public projection of a room (current temporal state).</summary>
public sealed record RoomResponse(
    Guid Id,
    string Iri,
    Guid BuildingId,
    string? Name,
    byte Floor,
    string? FunctionCode,
    string? ExposureCode,
    double? AreaM2,
    double? CeilingHeightM,
    string? VentilationType,
    IReadOnlyList<string> PollutionSources,
    string License);

/// <summary>A measured parameter with its QUDT quantity-kind and unit URIs.</summary>
public sealed record MeasuredParameterDto(
    string Code,
    string? QuantityKindUri,
    string? UnitUri)
{
    public static MeasuredParameterDto FromCode(string code)
    {
        var qudt = QudtVocabulary.TryResolve(code);
        return new MeasuredParameterDto(code, qudt?.QuantityKindUri, qudt?.UnitUri);
    }
}

/// <summary>Public projection of a sensor (current temporal state).</summary>
public sealed record SensorResponse(
    Guid Id,
    string Iri,
    Guid BuildingId,
    Guid RoomId,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? StatusCode,
    IReadOnlyList<MeasuredParameterDto> MeasuredParameters,
    string License);

/// <summary>
/// Offset-paged envelope for catalog entity lists (small sets). <see cref="Next"/>
/// is the absolute IRI of the next page or <c>null</c> when there are no more rows.
/// </summary>
public sealed record CatalogPage<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long Total,
    string? Next,
    string License);
