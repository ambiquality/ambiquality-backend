using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

/// <summary>
/// Relocates a sensor to a different room. The destination building is derived
/// from the target room so callers only supply the room id.
/// </summary>
public sealed class ChangeSensorPlacementHandler(
    ICurrentUser currentUser,
    ISensorRepository sensorRepository,
    IRoomRepository roomRepository,
    IBuildingRepository buildingRepository)
{
    public async Task Handle(ChangeSensorPlacementCommand command, CancellationToken ct)
    {
        var sensor = await SensorAuthorizer.LoadOwnedAsync(
            sensorRepository, buildingRepository, command.SensorId, currentUser, ct);

        var targetRoom = await roomRepository.GetByIdAsync(command.NewRoomId, ct)
            ?? throw new RoomNotFoundException(command.NewRoomId);

        await BuildingAuthorizer.LoadOwnedAsync(
            buildingRepository, targetRoom.BuildingId, currentUser, ct);

        sensor.ChangePlacement(targetRoom.BuildingId, targetRoom.Id, command.ValidFrom, currentUser.ProjectionId);
        await sensorRepository.SaveChangesAsync(ct);
    }
}
