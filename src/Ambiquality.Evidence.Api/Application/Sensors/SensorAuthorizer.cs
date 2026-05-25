using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

/// <summary>
/// Authorises sensor operations. Ownership derives from the sensor's current
/// building (its denormalised placement), so a caller may mutate a sensor only
/// if they own the building it currently sits in.
/// </summary>
internal static class SensorAuthorizer
{
    public static async Task<Sensor> LoadOwnedAsync(
        ISensorRepository sensorRepository,
        IBuildingRepository buildingRepository,
        Guid sensorId,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var sensor = await sensorRepository.GetByIdAsync(sensorId, cancellationToken)
            ?? throw new SensorNotFoundException(sensorId);

        await BuildingAuthorizer.LoadOwnedAsync(
            buildingRepository, sensor.CurrentBuildingId, currentUser, cancellationToken);

        return sensor;
    }
}
