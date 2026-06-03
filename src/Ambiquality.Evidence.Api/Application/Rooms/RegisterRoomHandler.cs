using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Rooms;

namespace Ambiquality.Evidence.Api.Application.Rooms;

public sealed class RegisterRoomHandler(
    IClock clock,
    ICurrentUser currentUser,
    IRoomRepository repository,
    IBuildingRepository buildingRepository,
    ISlugGenerator slugGenerator)
{
    public async Task<RegisterRoomResponse> Handle(RegisterRoomCommand command, CancellationToken ct)
    {
        RoomCodelists.ValidateExposure(command.ExposureCode);
        RoomCodelists.ValidateFunction(command.FunctionCode);
        RoomCodelists.ValidateVentilation(command.VentilationType);
        foreach (var source in command.PollutionSources)
            RoomCodelists.ValidatePollutionSource(source);

        await BuildingAuthorizer.LoadOwnedAsync(
            buildingRepository, command.BuildingId, currentUser, ct);

        var slug = await slugGenerator.NextAsync(
            "rm", repository.SlugExistsAsync, ct);

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
