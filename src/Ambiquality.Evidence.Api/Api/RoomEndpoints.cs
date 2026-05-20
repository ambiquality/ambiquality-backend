using Ambiquality.Evidence.Api.Application.Rooms;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Evidence.Api.Api;

public static class RoomEndpoints
{
    public static void MapRoomEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/buildings/{buildingId:guid}/rooms")
            .WithTags("Rooms");

        group.MapPost("/", RegisterRoom)
            .WithName("RegisterRoom")
            .WithOpenApi()
            .WithDescription("Register a new room in a building");

        group.MapGet("/{roomId:guid}", GetRoomById)
            .WithName("GetRoomById")
            .WithOpenApi()
            .WithDescription("Get a room by ID");

        group.MapGet("/{slug}", GetRoomBySlug)
            .WithName("GetRoomBySlug")
            .WithOpenApi()
            .WithDescription("Get a room by slug");

        group.MapPatch("/{roomId:guid}/name", ChangeRoomName)
            .WithName("ChangeRoomName")
            .WithOpenApi()
            .WithDescription("Change room name");

        group.MapPatch("/{roomId:guid}/floor", ChangeRoomFloor)
            .WithName("ChangeRoomFloor")
            .WithOpenApi()
            .WithDescription("Change room floor");

        group.MapPatch("/{roomId:guid}/function", ChangeRoomFunction)
            .WithName("ChangeRoomFunction")
            .WithOpenApi()
            .WithDescription("Change room function code");

        group.MapPatch("/{roomId:guid}/exposure", ChangeRoomExposure)
            .WithName("ChangeRoomExposure")
            .WithOpenApi()
            .WithDescription("Change room exposure category");

        group.MapPatch("/{roomId:guid}/geometry", ChangeRoomGeometry)
            .WithName("ChangeRoomGeometry")
            .WithOpenApi()
            .WithDescription("Change room geometry (area, ceiling height)");

        group.MapPatch("/{roomId:guid}/ventilation", ChangeRoomVentilation)
            .WithName("ChangeRoomVentilation")
            .WithOpenApi()
            .WithDescription("Change room ventilation type");

        group.MapPost("/{roomId:guid}/pollution-sources", AddPollutionSource)
            .WithName("AddPollutionSource")
            .WithOpenApi()
            .WithDescription("Add pollution source to room");

        group.MapDelete("/{roomId:guid}/pollution-sources/{sourceCode}", RemovePollutionSource)
            .WithName("RemovePollutionSource")
            .WithOpenApi()
            .WithDescription("Remove pollution source from room");
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
                UriSlug: request.UriSlug,
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

            return TypedResults.Created($"/buildings/{buildingId}/rooms/{result.RoomId}", response);
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<Results<Ok<RoomSnapshotResponse>, NotFound, ProblemHttpResult>> GetRoomById(
        Guid buildingId,
        Guid roomId,
        IRoomRepository repository,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var asOf = context.Request.Query["asOf"].FirstOrDefault() != null
                ? DateTime.Parse(context.Request.Query["asOf"].First()!)
                : DateTime.UtcNow;

            var room = await repository.GetByIdAsync(roomId, cancellationToken);
            if (room == null)
                return TypedResults.NotFound();

            if (room.BuildingId != buildingId)
                return TypedResults.NotFound();

            var snapshot = room.SnapshotAt(asOf);
            var response = new RoomSnapshotResponse(
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

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<Results<Ok<RoomSnapshotResponse>, NotFound, ProblemHttpResult>> GetRoomBySlug(
        Guid buildingId,
        string slug,
        IRoomRepository repository,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var asOf = context.Request.Query["asOf"].FirstOrDefault() != null
                ? DateTime.Parse(context.Request.Query["asOf"].First()!)
                : DateTime.UtcNow;

            var room = await repository.GetBySlugAsync(buildingId, UriSlug.Create(slug), cancellationToken);
            if (room == null)
                return TypedResults.NotFound();

            var snapshot = room.SnapshotAt(asOf);
            var response = new RoomSnapshotResponse(
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

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> ChangeRoomName(
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
            return TypedResults.Ok();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> ChangeRoomFloor(
        Guid buildingId,
        Guid roomId,
        ChangeRoomAttributeRequest request,
        ChangeRoomFloorHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeRoomFloorCommand(roomId, byte.Parse(request.NewValue), request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.Ok();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> ChangeRoomFunction(
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
            return TypedResults.Ok();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> ChangeRoomExposure(
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
            return TypedResults.Ok();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> ChangeRoomGeometry(
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
            return TypedResults.Ok();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> ChangeRoomVentilation(
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
            return TypedResults.Ok();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> AddPollutionSource(
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
            return TypedResults.Ok();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> RemovePollutionSource(
        Guid buildingId,
        Guid roomId,
        string sourceCode,
        RemoveRoomPollutionSourceHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var validToQuery = context.Request.Query["validTo"].ToString();
            var validTo = !string.IsNullOrEmpty(validToQuery)
                ? DateTime.Parse(validToQuery, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)
                : DateTime.UtcNow;

            var command = new RemoveRoomPollutionSourceCommand(roomId, sourceCode, validTo);
            await handler.Handle(command, cancellationToken);
            return TypedResults.Ok();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message);
        }
    }
}
