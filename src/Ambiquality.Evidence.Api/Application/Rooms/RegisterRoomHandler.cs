using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Rooms;

namespace Ambiquality.Evidence.Api.Application.Rooms;

public sealed class RegisterRoomHandler(
    IClock clock,
    ICurrentUser currentUser,
    IRoomRepository repository)
{
    public async Task<RegisterRoomResponse> Handle(RegisterRoomCommand command, CancellationToken ct)
    {
        var slug = UriSlug.Create(command.Name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-"));

        var room = Room.Register(
            slug: slug,
            buildingId: command.BuildingId,
            createdBy: currentUser.ProjectionId,
            name: command.Name,
            floor: FloorNumber.Create(command.Floor),
            functionCode: command.FunctionCode,
            exposureCode: command.ExposureCode,
            areaM2: command.AreaM2,
            ceilingHeightM: command.CeilingHeightM,
            ventilationType: command.VentilationType,
            pollutionSources: command.PollutionSources,
            now: clock.UtcNow);

        repository.Add(room);
        await repository.SaveChangesAsync(ct);

        return new RegisterRoomResponse(room.Id, room.UriSlug);
    }
}
