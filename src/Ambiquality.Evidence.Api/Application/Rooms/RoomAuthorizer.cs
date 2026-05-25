using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Rooms;

namespace Ambiquality.Evidence.Api.Application.Rooms;

/// <summary>
/// Authorises room operations. A room has no owner of its own — ownership lives
/// on the containing building, so a caller may mutate a room only if they own
/// that building.
/// </summary>
internal static class RoomAuthorizer
{
    public static async Task<Room> LoadOwnedAsync(
        IRoomRepository roomRepository,
        IBuildingRepository buildingRepository,
        Guid roomId,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var room = await roomRepository.GetByIdAsync(roomId, cancellationToken)
            ?? throw new RoomNotFoundException(roomId);

        // Loading the building through BuildingAuthorizer enforces the owner check.
        await BuildingAuthorizer.LoadOwnedAsync(
            buildingRepository, room.BuildingId, currentUser, cancellationToken);

        return room;
    }
}
