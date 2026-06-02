using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>
/// A measuring device installed in a room. The canonical device-registry
/// entity: its <see cref="Id"/> is the stable identity that ingested
/// measurements reference. Identity, placement, status and measured-parameter
/// capabilities are attribute-level temporal histories (see
/// <see cref="Common.Validity"/>); <see cref="CurrentBuildingId"/> /
/// <see cref="CurrentRoomId"/> denormalise the open placement row for fast
/// "sensors in this room" lookups.
/// </summary>
public sealed class Sensor
{
    private Sensor()
    {
        UriSlug = null!;
        ApiKeyHash = null!;
    }

    public Guid Id { get; private set; }
    public string UriSlug { get; private set; }
    public Guid CurrentBuildingId { get; private set; }
    public Guid CurrentRoomId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    /// <summary>
    /// SHA-256 hash of the sensor's API key. The plaintext is returned once at
    /// registration and never stored; ingestion authenticates by hashing the
    /// presented key and comparing against this value.
    /// </summary>
    public string ApiKeyHash { get; private set; }

    private readonly List<SensorIdentityHistory> _identityHistory = [];
    public IReadOnlyCollection<SensorIdentityHistory> IdentityHistory => _identityHistory.AsReadOnly();

    private readonly List<SensorPlacementHistory> _placementHistory = [];
    public IReadOnlyCollection<SensorPlacementHistory> PlacementHistory => _placementHistory.AsReadOnly();

    private readonly List<SensorStatusHistory> _statusHistory = [];
    public IReadOnlyCollection<SensorStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    private readonly List<SensorMeasuredParameterHistory> _measuredParameterHistory = [];
    public IReadOnlyCollection<SensorMeasuredParameterHistory> MeasuredParameterHistory => _measuredParameterHistory.AsReadOnly();

    public static Sensor Register(
        UriSlug slug,
        Guid buildingId,
        Guid roomId,
        Guid createdBy,
        string manufacturer,
        string model,
        string serialNumber,
        SensorStatus status,
        IReadOnlyCollection<MeasuredParameter> measuredParameters,
        string apiKeyHash,
        DateTime now)
    {
        var id = Guid.NewGuid();
        var validity = Validity.OpenFrom(now);

        var sensor = new Sensor
        {
            Id = id,
            UriSlug = slug.Value,
            CurrentBuildingId = buildingId,
            CurrentRoomId = roomId,
            CreatedAt = now,
            CreatedBy = createdBy,
            ApiKeyHash = apiKeyHash,
        };

        sensor._identityHistory.Add(new SensorIdentityHistory(id, validity, manufacturer, model, serialNumber, createdBy, now));
        sensor._placementHistory.Add(new SensorPlacementHistory(id, validity, buildingId, roomId, createdBy, now));
        sensor._statusHistory.Add(new SensorStatusHistory(id, validity, status.Code, createdBy, now));

        foreach (var parameter in measuredParameters)
        {
            sensor._measuredParameterHistory.Add(new SensorMeasuredParameterHistory(id, parameter.Code, validity, createdBy, now));
        }

        return sensor;
    }

    public void ChangeIdentity(string manufacturer, string model, string serialNumber, DateTime validFrom, Guid recordedBy)
    {
        var current = _identityHistory.Single(h => h.Validity.UpperBoundInfinite);

        // Idempotent replay: re-applying the same value at the same instant is a no-op.
        if (validFrom == current.Validity.LowerBound
            && current.Manufacturer == manufacturer
            && current.Model == model
            && current.SerialNumber == serialNumber)
            return;

        if (validFrom <= current.Validity.LowerBound)
            throw new DomainException("ValidFrom must be after the current open range's start");

        current.Close(validFrom);
        _identityHistory.Add(new SensorIdentityHistory(Id, Validity.OpenFrom(validFrom), manufacturer, model, serialNumber, recordedBy, validFrom));
    }

    public void ChangePlacement(Guid newBuildingId, Guid newRoomId, DateTime validFrom, Guid recordedBy)
    {
        var current = _placementHistory.Single(h => h.Validity.UpperBoundInfinite);

        // Idempotent replay: re-applying the same value at the same instant is a no-op
        // (also leaves the denormalised CurrentBuildingId/CurrentRoomId untouched).
        if (validFrom == current.Validity.LowerBound
            && current.BuildingId == newBuildingId
            && current.RoomId == newRoomId)
            return;

        if (validFrom <= current.Validity.LowerBound)
            throw new DomainException("ValidFrom must be after the current open range's start");

        current.Close(validFrom);
        _placementHistory.Add(new SensorPlacementHistory(Id, Validity.OpenFrom(validFrom), newBuildingId, newRoomId, recordedBy, validFrom));

        CurrentBuildingId = newBuildingId;
        CurrentRoomId = newRoomId;
    }

    public void ChangeStatus(SensorStatus newStatus, DateTime validFrom, Guid recordedBy)
    {
        var current = _statusHistory.Single(h => h.Validity.UpperBoundInfinite);

        // Idempotent replay: re-applying the same value at the same instant is a no-op.
        if (validFrom == current.Validity.LowerBound && current.StatusCode == newStatus.Code)
            return;

        if (validFrom <= current.Validity.LowerBound)
            throw new DomainException("ValidFrom must be after the current open range's start");

        current.Close(validFrom);
        _statusHistory.Add(new SensorStatusHistory(Id, Validity.OpenFrom(validFrom), newStatus.Code, recordedBy, validFrom));
    }

    public void AddMeasuredParameter(MeasuredParameter parameter, DateTime validFrom, Guid recordedBy)
    {
        var validity = Validity.OpenFrom(validFrom);
        _measuredParameterHistory.Add(new SensorMeasuredParameterHistory(Id, parameter.Code, validity, recordedBy, validFrom));
    }

    public void RemoveMeasuredParameter(string parameterCode, DateTime validTo)
    {
        var current = _measuredParameterHistory
            .Where(h => h.ParameterCode == parameterCode && h.Validity.UpperBoundInfinite)
            .SingleOrDefault();

        if (current is null)
            throw new MeasuredParameterNotFoundException(parameterCode);

        current.Close(validTo);
    }

    public SensorSnapshot SnapshotAt(DateTime asOf)
    {
        var identity = _identityHistory.Single(h => Validity.Covers(h.Validity, asOf));
        var placement = _placementHistory.Single(h => Validity.Covers(h.Validity, asOf));
        var status = _statusHistory.Single(h => Validity.Covers(h.Validity, asOf));

        return new SensorSnapshot(
            Id: Id,
            UriSlug: UriSlug,
            BuildingId: placement.BuildingId,
            RoomId: placement.RoomId,
            CreatedAt: CreatedAt,
            CreatedBy: CreatedBy,
            Manufacturer: identity.Manufacturer,
            Model: identity.Model,
            SerialNumber: identity.SerialNumber,
            StatusCode: status.StatusCode,
            MeasuredParameters: _measuredParameterHistory
                .Where(h => Validity.Covers(h.Validity, asOf))
                .Select(h => h.ParameterCode)
                .ToList(),
            AsOf: asOf);
    }
}

public sealed record SensorSnapshot(
    Guid Id,
    string UriSlug,
    Guid BuildingId,
    Guid RoomId,
    DateTime CreatedAt,
    Guid CreatedBy,
    string Manufacturer,
    string Model,
    string SerialNumber,
    string StatusCode,
    IReadOnlyCollection<string> MeasuredParameters,
    DateTime AsOf);
