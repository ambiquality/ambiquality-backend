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
    public static void MapBuildingEndpoints(this IEndpointRouteBuilder app)
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
            .WithDescription("Record a new building name effective from validFrom (appends history, does not overwrite)");

        group.MapPut("/{buildingId:guid}/address", ChangeBuildingAddress)
            .WithName("ChangeBuildingAddress")
            .WithOpenApi()
            .WithDescription("Record a new building address effective from validFrom (appends history, does not overwrite)");

        group.MapPut("/{buildingId:guid}/type", ChangeBuildingType)
            .WithName("ChangeBuildingType")
            .WithOpenApi()
            .WithDescription("Record a new building type effective from validFrom (appends history, does not overwrite)");

        group.MapPut("/{buildingId:guid}/location", ChangeBuildingLocation)
            .WithName("ChangeBuildingLocation")
            .WithOpenApi()
            .WithDescription("Record a new building location effective from validFrom (appends history, does not overwrite)");

        group.MapPut("/{buildingId:guid}/years", ChangeBuildingYears)
            .WithName("ChangeBuildingYears")
            .WithOpenApi()
            .WithDescription("Record new construction/renovation years effective from validFrom (appends history, does not overwrite)");
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
                AddressPointCode: request.AddressPointCode,
                StreetName: request.StreetName,
                HouseNumber: request.HouseNumber,
                HouseNumberType: request.HouseNumberType,
                OrientationNumber: request.OrientationNumber,
                OrientationNumberLetter: request.OrientationNumberLetter,
                MunicipalityName: request.MunicipalityName,
                MunicipalityPartName: request.MunicipalityPartName,
                Psc: request.Psc,
                DistrictName: request.DistrictName,
                RegionName: request.RegionName,
                BuildingTypeCode: request.BuildingTypeCode,
                Latitude: request.Latitude,
                Longitude: request.Longitude,
                YearBuilt: request.YearBuilt,
                YearRenovated: request.YearRenovated);

            var result = await handler.HandleAsync(command, cancellationToken);
            return TypedResults.Created($"/{Constants.ApiVersion}/buildings/{result.Id}", result);
        }
        catch (ArgumentException ex)
        {
            return Problems.InvalidAttributeValue(ex.Message);
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
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
            var responses = buildings
                .Select(b => ToResponse(b.SnapshotAt(asOf)))
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
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var asOfError = Problems.TryParseAsOf(context, out var asOf);
        if (asOfError is not null)
            return asOfError;

        var building = await repository.GetByIdAsync(buildingId, cancellationToken);
        if (building is null)
            return TypedResults.NotFound();

        try
        {
            return TypedResults.Ok(ToResponse(building.SnapshotAt(asOf)));
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static async Task<Results<Ok<BuildingSnapshotResponse>, NotFound, ProblemHttpResult>> GetBuildingBySlug(
        string slug,
        IBuildingRepository repository,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var asOfError = Problems.TryParseAsOf(context, out var asOf);
        if (asOfError is not null)
            return asOfError;

        var building = await repository.GetBySlugAsync(UriSlug.Create(slug), cancellationToken);
        if (building is null)
            return TypedResults.NotFound();

        try
        {
            return TypedResults.Ok(ToResponse(building.SnapshotAt(asOf)));
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
        }
    }

    private static BuildingSnapshotResponse ToResponse(BuildingSnapshot snapshot)
    {
        var address = snapshot.Address;
        return new(
            Id: snapshot.Id,
            UriSlug: snapshot.UriSlug,
            OwnerId: snapshot.OwnerId,
            Name: snapshot.Name,
            AddressPointCode: address.AddressPointCode,
            StreetName: address.StreetName,
            HouseNumber: address.HouseNumber,
            HouseNumberType: address.HouseNumberType,
            OrientationNumber: address.OrientationNumber,
            OrientationNumberLetter: address.OrientationNumberLetter,
            MunicipalityName: address.MunicipalityName,
            MunicipalityPartName: address.MunicipalityPartName,
            Psc: address.Psc,
            DistrictName: address.DistrictName,
            RegionName: address.RegionName,
            BuildingTypeCode: snapshot.BuildingTypeCode,
            Latitude: snapshot.Coordinates?.Latitude,
            Longitude: snapshot.Coordinates?.Longitude,
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
            return Problems.ToProblemResult(ex);
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
                AddressPointCode: request.AddressPointCode,
                StreetName: request.StreetName,
                HouseNumber: request.HouseNumber,
                HouseNumberType: request.HouseNumberType,
                OrientationNumber: request.OrientationNumber,
                OrientationNumberLetter: request.OrientationNumberLetter,
                MunicipalityName: request.MunicipalityName,
                MunicipalityPartName: request.MunicipalityPartName,
                Psc: request.Psc,
                DistrictName: request.DistrictName,
                RegionName: request.RegionName,
                ValidFrom: request.ValidFrom);

            await handler.HandleAsync(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (ArgumentException ex)
        {
            return Problems.InvalidAttributeValue(ex.Message);
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
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
            return Problems.ToProblemResult(ex);
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
                ValidFrom: request.ValidFrom);

            await handler.HandleAsync(command, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (ArgumentException ex)
        {
            return Problems.InvalidAttributeValue(ex.Message);
        }
        catch (DomainException ex)
        {
            return Problems.ToProblemResult(ex);
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
            return Problems.ToProblemResult(ex);
        }
    }
}

// Response DTOs
// Address fields follow the Czech OFN "Adresy" (2020-07-01) standard, anchored on
// the RÚIAN address-point code. Street name, orientation number, municipal part,
// district (okres) and region (kraj) are optional per the standard.
public sealed record BuildingSnapshotResponse(
    Guid Id,
    string UriSlug,
    Guid OwnerId,
    string Name,
    long AddressPointCode,
    string? StreetName,
    int HouseNumber,
    string HouseNumberType,
    int? OrientationNumber,
    string? OrientationNumberLetter,
    string MunicipalityName,
    string? MunicipalityPartName,
    string Psc,
    string? DistrictName,
    string? RegionName,
    string BuildingTypeCode,
    double? Latitude,
    double? Longitude,
    short? YearBuilt,
    short? YearRenovated,
    DateTime AsOf);

// Request DTOs
public sealed record RegisterBuildingRequest(
    string Name,
    long AddressPointCode,
    string? StreetName,
    int HouseNumber,
    string HouseNumberType,
    int? OrientationNumber,
    string? OrientationNumberLetter,
    string MunicipalityName,
    string? MunicipalityPartName,
    string Psc,
    string? DistrictName,
    string? RegionName,
    string BuildingTypeCode,
    double? Latitude,
    double? Longitude,
    short? YearBuilt,
    short? YearRenovated);

public sealed record ChangeBuildingNameRequest(string NewName, DateTime ValidFrom);

public sealed record ChangeBuildingAddressRequest(
    long AddressPointCode,
    string? StreetName,
    int HouseNumber,
    string HouseNumberType,
    int? OrientationNumber,
    string? OrientationNumberLetter,
    string MunicipalityName,
    string? MunicipalityPartName,
    string Psc,
    string? DistrictName,
    string? RegionName,
    DateTime ValidFrom);

public sealed record ChangeBuildingTypeRequest(string NewTypeCode, DateTime ValidFrom);

public sealed record ChangeBuildingLocationRequest(
    double? Latitude, double? Longitude, DateTime ValidFrom);

public sealed record ChangeBuildingYearsRequest(short? YearBuilt, short? YearRenovated, DateTime ValidFrom);
