using Ambiquality.Evidence.Api.Application.Rooms;
using Ambiquality.Evidence.Api.Domain;
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

        // GET + HEAD share one route. The modern AddOpenApi pipeline advertises
        // both methods from route metadata; the legacy .WithOpenApi() helper is
        // omitted here because it throws on multi-method routes.
        group.MapMethods("/{roomId:guid}", ["GET", "HEAD"], GetRoomById)
            .WithName("GetRoomById")
            .WithDescription("Get a room by ID");

        group.MapMethods("/{slug}", ["GET", "HEAD"], GetRoomBySlug)
            .WithName("GetRoomBySlug")
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

    private static async Task<Results<Ok, ProblemHttpResult>> ChangeRoomName(
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
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok, ProblemHttpResult>> ChangeRoomFloor(
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
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok, ProblemHttpResult>> ChangeRoomFunction(
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
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok, ProblemHttpResult>> ChangeRoomExposure(
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
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok, ProblemHttpResult>> ChangeRoomGeometry(
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
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok, ProblemHttpResult>> ChangeRoomVentilation(
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
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok, ProblemHttpResult>> AddPollutionSource(
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
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok, ProblemHttpResult>> RemovePollutionSource(
        Guid buildingId,
        Guid roomId,
        string sourceCode,
        RemoveRoomPollutionSourceHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validToError = Problems.TryParseValidTo(context, out var validTo);
        if (validToError is not null)
            return validToError;

        try
        {
            var command = new RemoveRoomPollutionSourceCommand(roomId, sourceCode, validTo);
            await handler.Handle(command, cancellationToken);
            return TypedResults.Ok();
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
