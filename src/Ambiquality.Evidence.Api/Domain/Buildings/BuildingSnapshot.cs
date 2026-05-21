using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// Point-in-time projection of a <see cref="Building"/> rebuilt from its
/// attribute history rows. Returned by <see cref="Building.SnapshotAt"/> and
/// fed to GET endpoints.
/// </summary>
public sealed record BuildingSnapshot(
    Guid Id,
    string UriSlug,
    Guid OwnerId,
    string Name,
    Address Address,
    string BuildingTypeCode,
    Coordinates? Coordinates,
    AnonymizationLevel Anonymization,
    short? YearBuilt,
    short? YearRenovated,
    DateTime AsOf);
