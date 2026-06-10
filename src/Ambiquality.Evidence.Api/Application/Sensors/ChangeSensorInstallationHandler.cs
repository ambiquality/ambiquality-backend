using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

/// <summary>
/// Records new installation details (F08) on a sensor — at registration via
/// <see cref="RegisterSensorHandler"/>, or subsequently here ("Doplňující údaje
/// lze zadávat při registraci i následně"). Closes the open installation row
/// half-open and opens a new one; a sensor with no installation row simply gets
/// its first one.
/// </summary>
public sealed class ChangeSensorInstallationHandler(
    ICurrentUser currentUser,
    ISensorRepository repository,
    IBuildingRepository buildingRepository)
{
    public async Task Handle(ChangeSensorInstallationCommand command, CancellationToken ct)
    {
        var sensor = await SensorAuthorizer.LoadOwnedAsync(
            repository, buildingRepository, command.SensorId, currentUser, ct);

        var details = command.Installation.ToDetails();

        sensor.ChangeInstallation(details, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(ct);
    }
}
