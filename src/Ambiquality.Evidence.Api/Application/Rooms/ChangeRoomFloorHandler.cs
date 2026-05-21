using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Rooms;

namespace Ambiquality.Evidence.Api.Application.Rooms;

public sealed class ChangeRoomFloorHandler(
    IClock clock,
    ICurrentUser currentUser,
    IRoomRepository repository)
{
    public async Task Handle(ChangeRoomFloorCommand command, CancellationToken ct)
    {
        var room = await repository.GetByIdAsync(command.RoomId, ct);
        if (room == null)
            throw new RoomNotFoundException(command.RoomId);

        room.ChangeFloor(FloorNumber.Create(command.NewFloor), command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(ct);
    }
}
