using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Application.Rooms;
using Ambiquality.Evidence.Api.Domain;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Evidence.Api.Api;

public static class RoomEndpoints
{
    public static void MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        // Mutations require a valid bearer token; reads opt out via AllowAnonymous.
        var group = app.MapGroup("/buildings/{buildingId:guid}/rooms")
            .WithTags("Rooms")
            .RequireAuthorization();

        group.MapPost("/", RegisterRoom)
            .WithName("RegisterRoom")
            .WithDescription("Register a new room in a building");

        // Owner-scoped listing: authenticated (no AllowAnonymous), requires the
        // caller to own the containing building.
        group.MapMethods("/", ["GET", "HEAD"], ListRooms)
            .WithName("ListRooms")
            .WithDescription("List the rooms of a building the caller owns");

        group.MapMethods("/{roomId:guid}", ["GET", "HEAD"], GetRoomById)
            .WithName("GetRoomById")
            .WithDescription("Get a room by ID")
            .AllowAnonymous();

        group.MapMethods("/{slug}", ["GET", "HEAD"], GetRoomBySlug)
            .WithName("GetRoomBySlug")
            .WithDescription("Get a room by slug")
            .AllowAnonymous();

        group.MapPut("/{roomId:guid}/name", ChangeRoomName)
            .WithName("ChangeRoomName")
            .WithDescription("Record a new room name effective from validFrom (appends history, does not overwrite)");

        group.MapPut("/{roomId:guid}/floor", ChangeRoomFloor)
            .WithName("ChangeRoomFloor")
            .WithDescription("Record a new room floor effective from validFrom (appends history, does not overwrite)");

        group.MapPut("/{roomId:guid}/function", ChangeRoomFunction)
            .WithName("ChangeRoomFunction")
            .WithDescription("Record a new room function effective from validFrom (appends history, does not overwrite)");

        group.MapPut("/{roomId:guid}/exposure", ChangeRoomExposure)
            .WithName("ChangeRoomExposure")
            .WithDescription("Record a new room exposure effective from validFrom (appends history, does not overwrite)");

        group.MapPut("/{roomId:guid}/geometry", ChangeRoomGeometry)
            .WithName("ChangeRoomGeometry")
            .WithDescription("Record new room geometry (area, ceiling height) effective from validFrom (appends history)");

        group.MapPut("/{roomId:guid}/ventilation", ChangeRoomVentilation)
            .WithName("ChangeRoomVentilation")
            .WithDescription("Record a new room ventilation type effective from validFrom (appends history, does not overwrite)");

        group.MapPost("/{roomId:guid}/pollution-sources", AddPollutionSource)
            .WithName("AddPollutionSource")
            .WithDescription("Record a pollution source effective from validFrom (appends history)");

        // PUT, not DELETE: closing a pollution source's validity period is a
        // soft-history mutation — nothing is physically removed (RFC 9110 §9.3.4
        // vs §9.3.5). The effective end instant travels in the body, uniform with
        // every other temporal mutation.
        group.MapPut("/{roomId:guid}/pollution-sources/{sourceCode}", RemovePollutionSource)
            .WithName("RemovePollutionSource")
            .WithDescription("Close a pollution source's validity as of validTo (soft history)");
    }

    private static async Task<Results<Created<RoomSnapshotResponse>, ProblemHttpResult>> RegisterRoom(
        Guid buildingId,
        RegisterRoomRequest request,
        RegisterRoomHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new RegisterRoomCommand(
                BuildingId: buildingId,
                Name: request.Name,
                Floor: request.Floor,
                FunctionCode: request.FunctionCode,
                ExposureCode: request.ExposureCode,
                AreaM2: request.AreaM2,
                CeilingHeightM: request.CeilingHeightM,
                VentilationType: request.VentilationType,
                PollutionSources: request.PollutionSources);

            var result = await handler.Handle(command, cancellationToken);
            var response = new RoomSnapshotResponse(
                Id: result.RoomId,
                UriSlug: result.UriSlug,
                BuildingId: buildingId,
                Name: request.Name,
                Floor: request.Floor,
                FunctionCode: request.FunctionCode,
                ExposureCode: request.ExposureCode,
                AreaM2: request.AreaM2,
                CeilingHeightM: request.CeilingHeightM,
                VentilationType: request.VentilationType,
                PollutionSources: request.PollutionSources,
                AsOf: DateTime.UtcNow);

            return TypedResults.Created($"/{Constants.ApiVersion}/buildings/{buildingId}/rooms/{result.RoomId}", response);
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok<IReadOnlyList<RoomSnapshotResponse>>, ProblemHttpResult>> ListRooms(
        Guid buildingId,
        IRoomRepository repository,
        IBuildingRepository buildingRepository,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var asOfError = Problems.TryParseAsOf(context, out var asOf);
        if (asOfError is not null)
            return asOfError;

        try
        {
            // Owner-scoped: rejects a non-owner (403) or unknown building (404)
            // before any room is read.
            await BuildingAuthorizer.LoadOwnedAsync(
                buildingRepository, buildingId, currentUser, cancellationToken);

            var rooms = await repository.GetByBuildingIdAsync(buildingId, cancellationToken);
            var responses = rooms
                .Select(r => ToResponse(r.SnapshotAt(asOf)))
                .ToList();
            return TypedResults.Ok((IReadOnlyList<RoomSnapshotResponse>)responses);
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok<RoomSnapshotResponse>, NotFound, ProblemHttpResult>> GetRoomById(
        Guid buildingId,
        Guid roomId,
        IRoomRepository repository,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var asOfError = Problems.TryParseAsOf(context, out var asOf);
        if (asOfError is not null)
            return asOfError;

        var room = await repository.GetByIdAsync(roomId, cancellationToken);
        if (room is null || room.BuildingId != buildingId)
            return TypedResults.NotFound();

        try
        {
            return TypedResults.Ok(ToResponse(room.SnapshotAt(asOf)));
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok<RoomSnapshotResponse>, NotFound, ProblemHttpResult>> GetRoomBySlug(
        Guid buildingId,
        string slug,
        IRoomRepository repository,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var asOfError = Problems.TryParseAsOf(context, out var asOf);
        if (asOfError is not null)
            return asOfError;

        var room = await repository.GetBySlugAsync(buildingId, UriSlug.Create(slug), cancellationToken);
        if (room is null)
            return TypedResults.NotFound();

        try
        {
            return TypedResults.Ok(ToResponse(room.SnapshotAt(asOf)));
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeRoomName(
        Guid buildingId,
        Guid roomId,
        ChangeRoomAttributeRequest request,
        ChangeRoomNameHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeRoomNameCommand(roomId, request.NewValue, request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeRoomFloor(
        Guid buildingId,
        Guid roomId,
        ChangeRoomFloorRequest request,
        ChangeRoomFloorHandler handler,
        CancellationToken cancellationToken)
    {
        // The framework already bound Floor as a byte (0–255), rejecting
        // non-numeric / negative / >255 input as 400. The domain accepts 0–100,
        // so 101–255 (a valid byte but FloorNumber.Create's ArgumentException,
        // which would 500) is rejected here as a 400.
        if (request.Floor > 100)
        {
            return Problems.InvalidAttributeValue(
                "Floor must be an integer between 0 and 100.");
        }

        try
        {
            var command = new ChangeRoomFloorCommand(roomId, request.Floor, request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeRoomFunction(
        Guid buildingId,
        Guid roomId,
        ChangeRoomAttributeRequest request,
        ChangeRoomFunctionHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeRoomFunctionCommand(roomId, request.NewValue, request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeRoomExposure(
        Guid buildingId,
        Guid roomId,
        ChangeRoomAttributeRequest request,
        ChangeRoomExposureHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeRoomExposureCommand(roomId, request.NewValue, request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeRoomGeometry(
        Guid buildingId,
        Guid roomId,
        ChangeRoomGeometryRequest request,
        ChangeRoomGeometryHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeRoomGeometryCommand(roomId, request.AreaM2, request.CeilingHeightM, request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeRoomVentilation(
        Guid buildingId,
        Guid roomId,
        ChangeRoomAttributeRequest request,
        ChangeRoomVentilationHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeRoomVentilationCommand(roomId, request.NewValue, request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> AddPollutionSource(
        Guid buildingId,
        Guid roomId,
        AddPollutionSourceRequest request,
        AddRoomPollutionSourceHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new AddRoomPollutionSourceCommand(roomId, request.SourceCode, request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemovePollutionSource(
        Guid buildingId,
        Guid roomId,
        string sourceCode,
        RemovePollutionSourceRequest request,
        RemoveRoomPollutionSourceHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new RemoveRoomPollutionSourceCommand(roomId, sourceCode, request.ValidTo);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static RoomSnapshotResponse ToResponse(RoomSnapshot snapshot) =>
        new(
            Id: snapshot.Id,
            UriSlug: snapshot.UriSlug,
            BuildingId: snapshot.BuildingId,
            Name: snapshot.Name,
            Floor: snapshot.Floor,
            FunctionCode: snapshot.FunctionCode,
            ExposureCode: snapshot.ExposureCode,
            AreaM2: snapshot.AreaM2,
            CeilingHeightM: snapshot.CeilingHeightM,
            VentilationType: snapshot.VentilationType,
            PollutionSources: snapshot.PollutionSources,
            AsOf: snapshot.AsOf);
}
