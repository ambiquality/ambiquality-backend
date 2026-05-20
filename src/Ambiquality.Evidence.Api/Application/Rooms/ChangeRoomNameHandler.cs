using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Rooms;

namespace Ambiquality.Evidence.Api.Application.Rooms;

public sealed class ChangeRoomNameHandler(
    IClock clock,
    ICurrentUser currentUser,
    IRoomRepository repository)
{
    public async Task Handle(ChangeRoomNameCommand command, CancellationToken ct)
    {
        var room = await repository.GetByIdAsync(command.RoomId, ct);
        if (room == null)
            throw new InvalidOperationException($"Room {command.RoomId} not found");

        room.ChangeName(command.NewName, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(ct);
    }
}
