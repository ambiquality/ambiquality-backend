using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Rooms;

namespace Ambiquality.Evidence.Api.Application.Rooms;

public sealed class RemoveRoomPollutionSourceHandler(
    IClock clock,
    ICurrentUser currentUser,
    IRoomRepository repository)
{
    public async Task Handle(RemoveRoomPollutionSourceCommand command, CancellationToken ct)
    {
        var room = await repository.GetByIdAsync(command.RoomId, ct);
        if (room == null)
            throw new RoomNotFoundException(command.RoomId);

        room.RemovePollutionSource(command.SourceCode, command.ValidTo);
        await repository.SaveChangesAsync(ct);
    }
}
