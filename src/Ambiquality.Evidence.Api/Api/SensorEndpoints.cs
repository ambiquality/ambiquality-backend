using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Application.Sensors;
using Ambiquality.Evidence.Api.Domain;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Ambiquality.Evidence.Api.Domain.Sensors;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Evidence.Api.Api;

public static class SensorEndpoints
{
    public static void MapSensorEndpoints(this IEndpointRouteBuilder app)
    {
        // Mutations require a valid bearer token; reads opt out via AllowAnonymous.
        var group = app.MapGroup("/buildings/{buildingId:guid}/rooms/{roomId:guid}/sensors")
            .WithTags("Sensors")
            .RequireAuthorization();

        group.MapPost("/", RegisterSensor)
            .WithName("RegisterSensor")
            .WithOpenApi()
            .WithDescription("Register a new sensor in a room");

        // Owner-scoped listing: authenticated (no AllowAnonymous), requires the
        // caller to own the containing building. GET + HEAD share the route, so
        // .WithOpenApi() is omitted (it throws on multi-method routes).
        group.MapMethods("/", ["GET", "HEAD"], ListSensors)
            .WithName("ListSensors")
            .WithDescription("List the sensors of a room in a building the caller owns");

        // GET + HEAD share one route; the AddOpenApi pipeline advertises both
        // methods from route metadata, so .WithOpenApi() is omitted here.
        group.MapMethods("/{sensorId:guid}", ["GET", "HEAD"], GetSensorById)
            .WithName("GetSensorById")
            .WithDescription("Get a sensor by ID")
            .AllowAnonymous();

        group.MapMethods("/{slug}", ["GET", "HEAD"], GetSensorBySlug)
            .WithName("GetSensorBySlug")
            .WithDescription("Get a sensor by slug")
            .AllowAnonymous();

        group.MapPut("/{sensorId:guid}/identity", ChangeSensorIdentity)
            .WithName("ChangeSensorIdentity")
            .WithOpenApi()
            .WithDescription("Record new sensor identity (manufacturer, model, serial) effective from validFrom (appends history)");

        group.MapPut("/{sensorId:guid}/placement", ChangeSensorPlacement)
            .WithName("ChangeSensorPlacement")
            .WithOpenApi()
            .WithDescription("Record a new sensor placement (room) effective from validFrom (appends history, does not overwrite)");

        group.MapPut("/{sensorId:guid}/status", ChangeSensorStatus)
            .WithName("ChangeSensorStatus")
            .WithOpenApi()
            .WithDescription("Record a new sensor lifecycle status effective from validFrom (appends history, does not overwrite)");

        group.MapPost("/{sensorId:guid}/measured-parameters", AddMeasuredParameter)
            .WithName("AddMeasuredParameter")
            .WithOpenApi()
            .WithDescription("Record a measured-parameter capability effective from validFrom (appends history)");

        // PUT, not DELETE: closing a capability's validity period is a soft-history
        // mutation — nothing is physically removed (RFC 9110 §9.3.4 vs §9.3.5). The
        // effective end instant travels in the body, uniform with every other
        // temporal mutation.
        group.MapPut("/{sensorId:guid}/measured-parameters/{parameterCode}", RemoveMeasuredParameter)
            .WithName("RemoveMeasuredParameter")
            .WithOpenApi()
            .WithDescription("Close a measured-parameter capability's validity as of validTo (soft history)");
    }

    private static async Task<Results<Created<SensorRegisteredResponse>, ProblemHttpResult>> RegisterSensor(
        Guid buildingId,
        Guid roomId,
        RegisterSensorRequest request,
        RegisterSensorHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new RegisterSensorCommand(
                BuildingId: buildingId,
                RoomId: roomId,
                Manufacturer: request.Manufacturer,
                Model: request.Model,
                SerialNumber: request.SerialNumber,
                StatusCode: request.StatusCode,
                MeasuredParameters: request.MeasuredParameters);

            var result = await handler.Handle(command, cancellationToken);
            var response = new SensorRegisteredResponse(
                Id: result.SensorId,
                UriSlug: result.UriSlug,
                BuildingId: buildingId,
                RoomId: roomId,
                Manufacturer: request.Manufacturer,
                Model: request.Model,
                SerialNumber: request.SerialNumber,
                StatusCode: request.StatusCode,
                MeasuredParameters: request.MeasuredParameters
                    .Select(MeasuredParameterResponse.FromCode)
                    .ToList(),
                AsOf: DateTime.UtcNow,
                ApiKey: result.ApiKey);

            return TypedResults.Created(
                $"/{Constants.ApiVersion}/buildings/{buildingId}/rooms/{roomId}/sensors/{result.SensorId}", response);
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok<IReadOnlyList<SensorSnapshotResponse>>, ProblemHttpResult>> ListSensors(
        Guid buildingId,
        Guid roomId,
        ISensorRepository repository,
        IRoomRepository roomRepository,
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
            // Owner-scoped: caller must own the building (403 otherwise, 404 if the
            // building is unknown).
            await BuildingAuthorizer.LoadOwnedAsync(
                buildingRepository, buildingId, currentUser, cancellationToken);

            // The room must exist and sit in the building named in the route.
            var room = await roomRepository.GetByIdAsync(roomId, cancellationToken);
            if (room is null || room.BuildingId != buildingId)
                return Problems.ToProblemResult(new RoomNotFoundException(roomId));

            var sensors = await repository.GetByRoomIdAsync(roomId, cancellationToken);
            var responses = new List<SensorSnapshotResponse>();
            foreach (var sensor in sensors)
            {
                // GetByRoomIdAsync filters on the denormalised current placement;
                // re-check the snapshot so an asOf in the past only includes sensors
                // that were actually in this room/building at that instant.
                var snapshot = sensor.SnapshotAt(asOf);
                if (snapshot.BuildingId == buildingId && snapshot.RoomId == roomId)
                    responses.Add(ToResponse(snapshot));
            }
            return TypedResults.Ok((IReadOnlyList<SensorSnapshotResponse>)responses);
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok<SensorSnapshotResponse>, NotFound, ProblemHttpResult>> GetSensorById(
        Guid buildingId,
        Guid roomId,
        Guid sensorId,
        ISensorRepository repository,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var asOfError = Problems.TryParseAsOf(context, out var asOf);
        if (asOfError is not null)
            return asOfError;

        var sensor = await repository.GetByIdAsync(sensorId, cancellationToken);
        if (sensor is null)
            return TypedResults.NotFound();

        return Project(sensor, buildingId, roomId, asOf);
    }

    private static async Task<Results<Ok<SensorSnapshotResponse>, NotFound, ProblemHttpResult>> GetSensorBySlug(
        Guid buildingId,
        Guid roomId,
        string slug,
        ISensorRepository repository,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var asOfError = Problems.TryParseAsOf(context, out var asOf);
        if (asOfError is not null)
            return asOfError;

        var sensor = await repository.GetBySlugAsync(UriSlug.Create(slug), cancellationToken);
        if (sensor is null)
            return TypedResults.NotFound();

        return Project(sensor, buildingId, roomId, asOf);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeSensorIdentity(
        Guid buildingId,
        Guid roomId,
        Guid sensorId,
        ChangeSensorIdentityRequest request,
        ChangeSensorIdentityHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeSensorIdentityCommand(
                sensorId, request.Manufacturer, request.Model, request.SerialNumber, request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeSensorPlacement(
        Guid buildingId,
        Guid roomId,
        Guid sensorId,
        ChangeSensorPlacementRequest request,
        ChangeSensorPlacementHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeSensorPlacementCommand(sensorId, request.NewRoomId, request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeSensorStatus(
        Guid buildingId,
        Guid roomId,
        Guid sensorId,
        ChangeSensorStatusRequest request,
        ChangeSensorStatusHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeSensorStatusCommand(sensorId, request.NewStatusCode, request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> AddMeasuredParameter(
        Guid buildingId,
        Guid roomId,
        Guid sensorId,
        AddMeasuredParameterRequest request,
        AddSensorMeasuredParameterHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new AddSensorMeasuredParameterCommand(sensorId, request.ParameterCode, request.ValidFrom);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveMeasuredParameter(
        Guid buildingId,
        Guid roomId,
        Guid sensorId,
        string parameterCode,
        RemoveMeasuredParameterRequest request,
        RemoveSensorMeasuredParameterHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new RemoveSensorMeasuredParameterCommand(sensorId, parameterCode, request.ValidTo);
            await handler.Handle(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    // Projects the sensor at the requested instant, returning 404 unless it was
    // placed in the path's room (and building) as of that time.
    private static Results<Ok<SensorSnapshotResponse>, NotFound, ProblemHttpResult> Project(
        Sensor sensor, Guid buildingId, Guid roomId, DateTime asOf)
    {
        try
        {
            var snapshot = sensor.SnapshotAt(asOf);
            if (snapshot.BuildingId != buildingId || snapshot.RoomId != roomId)
                return TypedResults.NotFound();

            return TypedResults.Ok(ToResponse(snapshot));
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static SensorSnapshotResponse ToResponse(SensorSnapshot snapshot) =>
        new(
            Id: snapshot.Id,
            UriSlug: snapshot.UriSlug,
            BuildingId: snapshot.BuildingId,
            RoomId: snapshot.RoomId,
            Manufacturer: snapshot.Manufacturer,
            Model: snapshot.Model,
            SerialNumber: snapshot.SerialNumber,
            StatusCode: snapshot.StatusCode,
            MeasuredParameters: snapshot.MeasuredParameters
                .Select(MeasuredParameterResponse.FromCode)
                .ToList(),
            AsOf: snapshot.AsOf);
}
