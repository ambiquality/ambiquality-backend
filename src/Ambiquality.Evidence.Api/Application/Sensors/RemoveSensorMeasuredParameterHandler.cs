using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

public sealed class RemoveSensorMeasuredParameterHandler(ISensorRepository repository)
{
    public async Task Handle(RemoveSensorMeasuredParameterCommand command, CancellationToken ct)
    {
        var sensor = await repository.GetByIdAsync(command.SensorId, ct)
            ?? throw new SensorNotFoundException(command.SensorId);

        sensor.RemoveMeasuredParameter(command.ParameterCode, command.ValidTo);
        await repository.SaveChangesAsync(ct);
    }
}
