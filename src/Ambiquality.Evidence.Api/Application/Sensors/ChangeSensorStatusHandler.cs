using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

public sealed class ChangeSensorStatusHandler(
    ICurrentUser currentUser,
    ISensorRepository repository)
{
    public async Task Handle(ChangeSensorStatusCommand command, CancellationToken ct)
    {
        var sensor = await repository.GetByIdAsync(command.SensorId, ct)
            ?? throw new SensorNotFoundException(command.SensorId);

        var status = SensorCodelists.ParseStatus(command.NewStatusCode);
        sensor.ChangeStatus(status, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(ct);
    }
}
