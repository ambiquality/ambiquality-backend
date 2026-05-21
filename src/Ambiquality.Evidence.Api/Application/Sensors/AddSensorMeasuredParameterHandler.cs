using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

public sealed class AddSensorMeasuredParameterHandler(
    ICurrentUser currentUser,
    ISensorRepository repository)
{
    public async Task Handle(AddSensorMeasuredParameterCommand command, CancellationToken ct)
    {
        var sensor = await repository.GetByIdAsync(command.SensorId, ct)
            ?? throw new SensorNotFoundException(command.SensorId);

        var parameter = SensorCodelists.ParseParameter(command.ParameterCode);
        sensor.AddMeasuredParameter(parameter, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(ct);
    }
}
