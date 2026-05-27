using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

public sealed class RegisterSensorHandler(
    IClock clock,
    ICurrentUser currentUser,
    ISensorRepository repository,
    IBuildingRepository buildingRepository,
    ISensorApiKeyService apiKeyService)
{
    public async Task<RegisterSensorResponse> Handle(RegisterSensorCommand command, CancellationToken ct)
    {
        await BuildingAuthorizer.LoadOwnedAsync(
            buildingRepository, command.BuildingId, currentUser, ct);

        var slug = UriSlug.Create(command.UriSlug);
        var status = SensorCodelists.ParseStatus(command.StatusCode);
        var parameters = command.MeasuredParameters.Select(SensorCodelists.ParseParameter).ToList();

        var (apiKey, apiKeyHash) = apiKeyService.Generate();

        var sensor = Sensor.Register(
            slug: slug,
            buildingId: command.BuildingId,
            roomId: command.RoomId,
            createdBy: currentUser.ProjectionId,
            manufacturer: command.Manufacturer,
            model: command.Model,
            serialNumber: command.SerialNumber,
            status: status,
            measuredParameters: parameters,
            apiKeyHash: apiKeyHash,
            now: clock.UtcNow);

        repository.Add(sensor);
        await repository.SaveChangesAsync(ct);

        return new RegisterSensorResponse(sensor.Id, sensor.UriSlug, apiKey);
    }
}
