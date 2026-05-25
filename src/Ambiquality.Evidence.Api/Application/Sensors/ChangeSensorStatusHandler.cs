using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

public sealed class ChangeSensorStatusHandler(
    ICurrentUser currentUser,
    ISensorRepository repository,
    IBuildingRepository buildingRepository)
{
    public async Task Handle(ChangeSensorStatusCommand command, CancellationToken ct)
    {
        var sensor = await SensorAuthorizer.LoadOwnedAsync(
            repository, buildingRepository, command.SensorId, currentUser, ct);

        var status = SensorCodelists.ParseStatus(command.NewStatusCode);
        sensor.ChangeStatus(status, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(ct);
    }
}
