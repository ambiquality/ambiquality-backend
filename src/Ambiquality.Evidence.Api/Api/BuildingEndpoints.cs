using Ambiquality.Evidence.Api.Application;
using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Domain;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Evidence.Api.Api;

public static class BuildingEndpoints
{
    public static void MapBuildingEndpoints(this WebApplication app)
    {
        // Mutations require a valid bearer token; reads opt out via AllowAnonymous
        // below (the open-data catalog is publicly readable). Authentication still
        // runs on reads so an authenticated owner can be recognised.
        var group = app.MapGroup("/buildings")
            .WithTags("Buildings")
            .RequireAuthorization();

        group.MapPost("/", RegisterBuilding)
            .WithName("RegisterBuilding")
            .WithOpenApi()
            .WithDescription("Register a new building");

        // Owner-scoped catalog listing: authenticated (no AllowAnonymous), returns
        // only the caller's own buildings with unmasked coordinates. The public,
        // masked listing lives in Public.Api. GET + HEAD share the route, so
        // .WithOpenApi() is omitted (it throws on multi-method routes).
        group.MapMethods("/", ["GET", "HEAD"], ListBuildings)
            .WithName("ListBuildings")
            .WithDescription("List the authenticated owner's buildings (unmasked coordinates)");

        // GET + HEAD share one route. The modern AddOpenApi pipeline advertises
        // both methods from route metadata; the legacy .WithOpenApi() helper is
        // omitted here because it throws on multi-method routes.
        group.MapMethods("/{buildingId:guid}", ["GET", "HEAD"], GetBuildingById)
            .WithName("GetBuildingById")
            .WithDescription("Get a building by ID")
            .AllowAnonymous();

        group.MapMethods("/{slug}", ["GET", "HEAD"], GetBuildingBySlug)
            .WithName("GetBuildingBySlug")
            .WithDescription("Get a building by slug")
            .AllowAnonymous();

        group.MapPut("/{buildingId:guid}/name", ChangeBuildingName)
            .WithName("ChangeBuildingName")
            .WithOpenApi()
            .WithDescription("Change a building's name");

        group.MapPut("/{buildingId:guid}/address", ChangeBuildingAddress)
            .WithName("ChangeBuildingAddress")
            .WithOpenApi()
            .WithDescription("Change a building's address");

        group.MapPut("/{buildingId:guid}/type", ChangeBuildingType)
            .WithName("ChangeBuildingType")
            .WithOpenApi()
            .WithDescription("Change a building's type");

        group.MapPut("/{buildingId:guid}/location", ChangeBuildingLocation)
            .WithName("ChangeBuildingLocation")
            .WithOpenApi()
            .WithDescription("Change a building's location");

        group.MapPut("/{buildingId:guid}/years", ChangeBuildingYears)
            .WithName("ChangeBuildingYears")
            .WithOpenApi()
            .WithDescription("Change a building's construction and renovation years");
    }

    private static async Task<Results<Created<RegisterBuildingResult>, ProblemHttpResult>> RegisterBuilding(
        RegisterBuildingRequest request,
        RegisterBuildingHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new RegisterBuildingCommand(
                Name: request.Name,
                Street: request.Street,
                City: request.City,
                Postcode: request.Postcode,
                Country: request.Country,
                BuildingTypeCode: request.BuildingTypeCode,
                Latitude: request.Latitude,
                Longitude: request.Longitude,
                AnonymizationLevel: request.AnonymizationLevel,
                YearBuilt: request.YearBuilt,
                YearRenovated: request.YearRenovated);

            var result = await handler.HandleAsync(command, cancellationToken);
            return TypedResults.Created($"/buildings/{result.Id}", result);
        }
        catch (DomainException ex)
        {
            var problem = Problems.Describe(ex);
            return TypedResults.Problem(
                detail: problem.Detail,
                title: problem.Title,
                type: problem.Type,
                statusCode: problem.StatusCode);
        }
    }

    private static async Task<Results<Ok<IReadOnlyList<BuildingSnapshotResponse>>, ProblemHttpResult>> ListBuildings(
        IBuildingRepository repository,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var asOfError = Problems.TryParseAsOf(context, out var asOf);
        if (asOfError is not null)
            return asOfError;

        var buildings = await repository.ListOwnedByAsync(currentUser.ProjectionId, cancellationToken);

        try
        {
            // Every returned building is the caller's own, so isOwner is true and
            // coordinates are never masked.
            var responses = buildings
                .Select(b => ToResponse(b.SnapshotAt(asOf), isOwner: true))
                .ToList();
            return TypedResults.Ok((IReadOnlyList<BuildingSnapshotResponse>)responses);
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok<BuildingSnapshotResponse>, NotFound, ProblemHttpResult>> GetBuildingById(
        Guid buildingId,
        IBuildingRepository repository,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var asOfError = Problems.TryParseAsOf(context, out var asOf);
        if (asOfError is not null)
            return asOfError;

        var building = await repository.GetByIdAsync(buildingId, cancellationToken);
        if (building is null)
            return TypedResults.NotFound();

        var isOwner = currentUser.IsAuthenticated && building.OwnerId == currentUser.ProjectionId;

        try
        {
            return TypedResults.Ok(ToResponse(building.SnapshotAt(asOf), isOwner));
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok<BuildingSnapshotResponse>, NotFound, ProblemHttpResult>> GetBuildingBySlug(
        string slug,
        IBuildingRepository repository,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var asOfError = Problems.TryParseAsOf(context, out var asOf);
        if (asOfError is not null)
            return asOfError;

        var building = await repository.GetBySlugAsync(UriSlug.Create(slug), cancellationToken);
        if (building is null)
            return TypedResults.NotFound();

        var isOwner = currentUser.IsAuthenticated && building.OwnerId == currentUser.ProjectionId;

        try
        {
            return TypedResults.Ok(ToResponse(building.SnapshotAt(asOf), isOwner));
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static BuildingSnapshotResponse ToResponse(BuildingSnapshot snapshot, bool isOwner)
    {
        var (latitude, longitude) = CoordinateMasking.Apply(
            snapshot.Coordinates?.Latitude,
            snapshot.Coordinates?.Longitude,
            snapshot.Anonymization.Code,
            isOwner);

        return new(
            Id: snapshot.Id,
            UriSlug: snapshot.UriSlug,
            OwnerId: snapshot.OwnerId,
            Name: snapshot.Name,
            Street: snapshot.Address.Street,
            City: snapshot.Address.City,
            Postcode: snapshot.Address.Postcode,
            Country: snapshot.Address.Country,
            BuildingTypeCode: snapshot.BuildingTypeCode,
            Latitude: latitude,
            Longitude: longitude,
            AnonymizationLevel: snapshot.Anonymization.Code,
            YearBuilt: snapshot.YearBuilt,
            YearRenovated: snapshot.YearRenovated,
            AsOf: snapshot.AsOf);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeBuildingName(
        Guid buildingId,
        ChangeBuildingNameRequest request,
        ChangeBuildingNameHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeBuildingNameCommand(
                BuildingId: buildingId,
                NewName: request.NewName,
                ValidFrom: request.ValidFrom);

            await handler.HandleAsync(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            var problem = Problems.Describe(ex);
            return TypedResults.Problem(
                detail: problem.Detail,
                title: problem.Title,
                type: problem.Type,
                statusCode: problem.StatusCode);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeBuildingAddress(
        Guid buildingId,
        ChangeBuildingAddressRequest request,
        ChangeBuildingAddressHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeBuildingAddressCommand(
                BuildingId: buildingId,
                Street: request.Street,
                City: request.City,
                Postcode: request.Postcode,
                Country: request.Country,
                ValidFrom: request.ValidFrom);

            await handler.HandleAsync(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            var problem = Problems.Describe(ex);
            return TypedResults.Problem(
                detail: problem.Detail,
                title: problem.Title,
                type: problem.Type,
                statusCode: problem.StatusCode);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeBuildingType(
        Guid buildingId,
        ChangeBuildingTypeRequest request,
        ChangeBuildingTypeHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeBuildingTypeCommand(
                BuildingId: buildingId,
                NewTypeCode: request.NewTypeCode,
                ValidFrom: request.ValidFrom);

            await handler.HandleAsync(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            var problem = Problems.Describe(ex);
            return TypedResults.Problem(
                detail: problem.Detail,
                title: problem.Title,
                type: problem.Type,
                statusCode: problem.StatusCode);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeBuildingLocation(
        Guid buildingId,
        ChangeBuildingLocationRequest request,
        ChangeBuildingLocationHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeBuildingLocationCommand(
                BuildingId: buildingId,
                Latitude: request.Latitude,
                Longitude: request.Longitude,
                AnonymizationLevel: request.AnonymizationLevel,
                ValidFrom: request.ValidFrom);

            await handler.HandleAsync(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            var problem = Problems.Describe(ex);
            return TypedResults.Problem(
                detail: problem.Detail,
                title: problem.Title,
                type: problem.Type,
                statusCode: problem.StatusCode);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeBuildingYears(
        Guid buildingId,
        ChangeBuildingYearsRequest request,
        ChangeBuildingYearsHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ChangeBuildingYearsCommand(
                BuildingId: buildingId,
                YearBuilt: request.YearBuilt,
                YearRenovated: request.YearRenovated,
                ValidFrom: request.ValidFrom);

            await handler.HandleAsync(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (DomainException ex)
        {
            var problem = Problems.Describe(ex);
            return TypedResults.Problem(
                detail: problem.Detail,
                title: problem.Title,
                type: problem.Type,
                statusCode: problem.StatusCode);
        }
    }
}

// Response DTOs
public sealed record BuildingSnapshotResponse(
    Guid Id,
    string UriSlug,
    Guid OwnerId,
    string Name,
    string Street,
    string City,
    string Postcode,
    string Country,
    string BuildingTypeCode,
    double? Latitude,
    double? Longitude,
    string AnonymizationLevel,
    short? YearBuilt,
    short? YearRenovated,
    DateTime AsOf);

// Request DTOs
public sealed record RegisterBuildingRequest(
    string Name,
    string Street,
    string City,
    string Postcode,
    string Country,
    string BuildingTypeCode,
    double? Latitude,
    double? Longitude,
    string AnonymizationLevel,
    short? YearBuilt,
    short? YearRenovated);

public sealed record ChangeBuildingNameRequest(string NewName, DateTime ValidFrom);

public sealed record ChangeBuildingAddressRequest(
    string Street, string City, string Postcode, string Country, DateTime ValidFrom);

public sealed record ChangeBuildingTypeRequest(string NewTypeCode, DateTime ValidFrom);

public sealed record ChangeBuildingLocationRequest(
    double? Latitude, double? Longitude, string AnonymizationLevel, DateTime ValidFrom);

public sealed record ChangeBuildingYearsRequest(short? YearBuilt, short? YearRenovated, DateTime ValidFrom);
