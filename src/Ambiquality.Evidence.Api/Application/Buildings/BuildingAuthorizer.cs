using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;

namespace Ambiquality.Evidence.Api.Application.Buildings;

/// <summary>
/// Shared authorisation helper: loads a building by id and rejects callers
/// that are not its owner. Centralised so every Change handler enforces the
/// same rule without duplicating boilerplate.
/// </summary>
internal static class BuildingAuthorizer
{
    public static async Task<Building> LoadOwnedAsync(
        IBuildingRepository repository,
        Guid buildingId,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var building = await repository.GetByIdAsync(buildingId, cancellationToken)
            ?? throw new BuildingNotFoundException();

        if (building.OwnerId != currentUser.ProjectionId)
            throw new ForbiddenException();

        return building;
    }
}
