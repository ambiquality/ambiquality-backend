using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Application.Buildings;

/// <summary>
/// UC05 — registers a new building owned by the authenticated user. Builds
/// the aggregate, persists it, and surfaces a DB-side slug collision as a
/// <see cref="DuplicateUriSlugException"/>.
/// </summary>
public sealed class RegisterBuildingHandler(
    IBuildingRepository repository,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<RegisterBuildingResult> HandleAsync(
        RegisterBuildingCommand command, CancellationToken cancellationToken = default)
    {
        var slug = UriSlug.Create(command.UriSlug);

        // First-line slug check (DB has a UNIQUE index as the source of truth).
        if (await repository.GetBySlugAsync(slug, cancellationToken) is not null)
            throw new DuplicateUriSlugException();

        var address = Address.Create(command.Street, command.City, command.Postcode, command.Country);
        var anonymization = ParseAnonymization(command.AnonymizationLevel);
        var coordinates = ParseCoordinates(command.Latitude, command.Longitude);

        var building = Building.Register(
            slug: slug,
            ownerId: currentUser.ProjectionId,
            createdBy: currentUser.ProjectionId,
            name: command.Name,
            address: address,
            buildingTypeCode: command.BuildingTypeCode,
            coordinates: coordinates,
            anonymization: anonymization,
            yearBuilt: command.YearBuilt,
            yearRenovated: command.YearRenovated,
            now: clock.UtcNow);

        repository.Add(building);
        await repository.SaveChangesAsync(cancellationToken);

        return new RegisterBuildingResult(building.Id, building.UriSlug);
    }

    internal static AnonymizationLevel ParseAnonymization(string code)
    {
        try
        {
            return AnonymizationLevel.FromCode(code);
        }
        catch (ArgumentException)
        {
            throw new UnknownCodelistCodeException("anonymization_level", code);
        }
    }

    internal static Coordinates? ParseCoordinates(double? lat, double? lon)
    {
        if (lat is null && lon is null) return null;
        if (lat is null || lon is null)
            throw new ArgumentException(
                "Latitude and longitude must be supplied together or both omitted.");
        return Coordinates.Create(lat.Value, lon.Value);
    }
}
