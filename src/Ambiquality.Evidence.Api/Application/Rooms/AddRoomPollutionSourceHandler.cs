using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Rooms;

namespace Ambiquality.Evidence.Api.Application.Rooms;

public sealed class AddRoomPollutionSourceHandler(
    IClock clock,
    ICurrentUser currentUser,
    IRoomRepository repository,
    IBuildingRepository buildingRepository)
{
    public async Task Handle(AddRoomPollutionSourceCommand command, CancellationToken ct)
    {
        var room = await RoomAuthorizer.LoadOwnedAsync(
            repository, buildingRepository, command.RoomId, currentUser, ct);

        room.AddPollutionSource(command.SourceCode, command.ValidFrom);
        await repository.SaveChangesAsync(ct);
    }
}
