using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Rooms;

namespace Ambiquality.Evidence.Api.Application.Rooms;

public sealed class ChangeRoomVentilationHandler(
    ICurrentUser currentUser,
    IRoomRepository repository,
    IBuildingRepository buildingRepository)
{
    public async Task Handle(ChangeRoomVentilationCommand command, CancellationToken ct)
    {
        RoomCodelists.ValidateVentilation(command.NewVentilationType);

        var room = await RoomAuthorizer.LoadOwnedAsync(
            repository, buildingRepository, command.RoomId, currentUser, ct);

        room.ChangeVentilation(command.NewVentilationType, command.ValidFrom, currentUser.ProjectionId);
        await repository.SaveChangesAsync(ct);
    }
}
