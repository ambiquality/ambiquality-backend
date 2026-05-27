using Ambiquality.Evidence.Api.Application.Sensors;
using Ambiquality.Evidence.Api.Domain;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Sensors;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Evidence.Api.Api;

public static class SensorEndpoints
{
    public static void MapSensorEndpoints(this WebApplication app)
    {
        // Mutations require a valid bearer token; reads opt out via AllowAnonymous.
        var group = app.MapGroup("/buildings/{buildingId:guid}/rooms/{roomId:guid}/sensors")
            .WithTags("Sensors")
            .RequireAuthorization();

        group.MapPost("/", RegisterSensor)
            .WithName("RegisterSensor")
            .WithOpenApi()
            .WithDescription("Register a new sensor in a room");

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
            .WithDescription("Change sensor hardware identity (manufacturer, model, serial)");

        group.MapPut("/{sensorId:guid}/placement", ChangeSensorPlacement)
            .WithName("ChangeSensorPlacement")
            .WithOpenApi()
            .WithDescription("Relocate the sensor to a different room");

        group.MapPut("/{sensorId:guid}/status", ChangeSensorStatus)
            .WithName("ChangeSensorStatus")
            .WithOpenApi()
            .WithDescription("Change sensor lifecycle status");

        group.MapPost("/{sensorId:guid}/measured-parameters", AddMeasuredParameter)
            .WithName("AddMeasuredParameter")
            .WithOpenApi()
            .WithDescription("Add a measured parameter capability to the sensor");

        group.MapDelete("/{sensorId:guid}/measured-parameters/{parameterCode}", RemoveMeasuredParameter)
            .WithName("RemoveMeasuredParameter")
            .WithOpenApi()
            .WithDescription("Remove a measured parameter capability from the sensor");
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
                UriSlug: request.UriSlug,
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
                MeasuredParameters: request.MeasuredParameters,
                AsOf: DateTime.UtcNow,
                ApiKey: result.ApiKey);

            return TypedResults.Created(
                $"/buildings/{buildingId}/rooms/{roomId}/sensors/{result.SensorId}", response);
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

    private static async Task<Results<Ok, ProblemHttpResult>> AddMeasuredParameter(
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
            return TypedResults.Ok();
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok, ProblemHttpResult>> RemoveMeasuredParameter(
        Guid buildingId,
        Guid roomId,
        Guid sensorId,
        string parameterCode,
        RemoveSensorMeasuredParameterHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validToError = Problems.TryParseValidTo(context, out var validTo);
        if (validToError is not null)
            return validToError;

        try
        {
            var command = new RemoveSensorMeasuredParameterCommand(sensorId, parameterCode, validTo);
            await handler.Handle(command, cancellationToken);
            return TypedResults.Ok();
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
            MeasuredParameters: snapshot.MeasuredParameters,
            AsOf: snapshot.AsOf);
}
