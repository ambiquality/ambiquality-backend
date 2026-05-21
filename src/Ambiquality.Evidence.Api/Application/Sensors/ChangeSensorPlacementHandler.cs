using Ambiquality.Evidence.Api.Application.Abstractions;
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
    IRoomRepository roomRepository)
{
    public async Task Handle(ChangeSensorPlacementCommand command, CancellationToken ct)
    {
        var sensor = await sensorRepository.GetByIdAsync(command.SensorId, ct)
            ?? throw new SensorNotFoundException(command.SensorId);

        var targetRoom = await roomRepository.GetByIdAsync(command.NewRoomId, ct)
            ?? throw new RoomNotFoundException(command.NewRoomId);

        sensor.ChangePlacement(targetRoom.BuildingId, targetRoom.Id, command.ValidFrom, currentUser.ProjectionId);
        await sensorRepository.SaveChangesAsync(ct);
    }
}
