using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

public sealed class RemoveSensorMeasuredParameterHandler(
    ICurrentUser currentUser,
    ISensorRepository repository,
    IBuildingRepository buildingRepository)
{
    public async Task Handle(RemoveSensorMeasuredParameterCommand command, CancellationToken ct)
    {
        var sensor = await SensorAuthorizer.LoadOwnedAsync(
            repository, buildingRepository, command.SensorId, currentUser, ct);

        sensor.RemoveMeasuredParameter(command.ParameterCode, command.ValidTo);
        await repository.SaveChangesAsync(ct);
    }
}
