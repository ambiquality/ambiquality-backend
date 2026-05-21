using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

public sealed class ChangeSensorIdentityHandler(
    ICurrentUser currentUser,
    ISensorRepository repository)
{
    public async Task Handle(ChangeSensorIdentityCommand command, CancellationToken ct)
    {
        var sensor = await repository.GetByIdAsync(command.SensorId, ct)
            ?? throw new SensorNotFoundException(command.SensorId);

        sensor.ChangeIdentity(command.Manufacturer, command.Model, command.SerialNumber, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(ct);
    }
}
