using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;

namespace Ambiquality.Evidence.Api.Application.Buildings;

/// <summary>UC07 — change a building's postal address, closing the previous range.</summary>
public sealed class ChangeBuildingAddressHandler(
    IBuildingRepository repository,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(ChangeBuildingAddressCommand command, CancellationToken cancellationToken = default)
    {
        var building = await BuildingAuthorizer.LoadOwnedAsync(
            repository, command.BuildingId, currentUser, cancellationToken);
        var address = Address.Create(command.Street, command.City, command.Postcode, command.Country);
        building.ChangeAddress(address, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
