using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;

namespace Ambiquality.Evidence.Api.Application.Buildings;

/// <summary>UC07 — change a building's type, closing the previous range.</summary>
public sealed class ChangeBuildingTypeHandler(
    IBuildingRepository repository,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(ChangeBuildingTypeCommand command, CancellationToken cancellationToken = default)
    {
        BuildingCodelists.ValidateType(command.NewTypeCode);

        var building = await BuildingAuthorizer.LoadOwnedAsync(
            repository, command.BuildingId, currentUser, cancellationToken);
        building.ChangeType(command.NewTypeCode, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
