using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

public sealed class ChangeSensorIdentityHandler(
    ICurrentUser currentUser,
    ISensorRepository repository,
    IBuildingRepository buildingRepository)
{
    public async Task Handle(ChangeSensorIdentityCommand command, CancellationToken ct)
    {
        var sensor = await SensorAuthorizer.LoadOwnedAsync(
            repository, buildingRepository, command.SensorId, currentUser, ct);

        sensor.ChangeIdentity(command.Manufacturer, command.Model, command.SerialNumber, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(ct);
    }
}
