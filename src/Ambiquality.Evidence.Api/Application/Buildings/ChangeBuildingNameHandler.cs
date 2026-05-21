using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;

namespace Ambiquality.Evidence.Api.Application.Buildings;

/// <summary>UC07 — change a building's display name, closing the previous range.</summary>
public sealed class ChangeBuildingNameHandler(
    IBuildingRepository repository,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(ChangeBuildingNameCommand command, CancellationToken cancellationToken = default)
    {
        var building = await BuildingAuthorizer.LoadOwnedAsync(
            repository, command.BuildingId, currentUser, cancellationToken);
        building.ChangeName(command.NewName, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
