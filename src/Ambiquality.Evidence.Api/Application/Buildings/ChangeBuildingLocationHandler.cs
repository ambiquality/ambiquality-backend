using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;

namespace Ambiquality.Evidence.Api.Application.Buildings;

/// <summary>UC07 — change a building's location and / or anonymization level.</summary>
public sealed class ChangeBuildingLocationHandler(
    IBuildingRepository repository,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(ChangeBuildingLocationCommand command, CancellationToken cancellationToken = default)
    {
        var building = await BuildingAuthorizer.LoadOwnedAsync(
            repository, command.BuildingId, currentUser, cancellationToken);
        var anonymization = RegisterBuildingHandler.ParseAnonymization(command.AnonymizationLevel);
        var coordinates = RegisterBuildingHandler.ParseCoordinates(command.Latitude, command.Longitude);
        building.ChangeLocation(coordinates, anonymization, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
