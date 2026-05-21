using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;

namespace Ambiquality.Evidence.Api.Application.Buildings;

/// <summary>UC07 — update a building's year-built / year-renovated tuple.</summary>
public sealed class ChangeBuildingYearsHandler(
    IBuildingRepository repository,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(ChangeBuildingYearsCommand command, CancellationToken cancellationToken = default)
    {
        var building = await BuildingAuthorizer.LoadOwnedAsync(
            repository, command.BuildingId, currentUser, cancellationToken);
        building.ChangeYears(command.YearBuilt, command.YearRenovated, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
