using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class Room
{
    private Room()
    {
        UriSlug = null!;
    }

    public Guid Id { get; private set; }
    public string UriSlug { get; private set; }
    public Guid BuildingId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private readonly List<RoomNameHistory> _nameHistory = [];
    public IReadOnlyCollection<RoomNameHistory> NameHistory => _nameHistory.AsReadOnly();

    private readonly List<RoomFloorHistory> _floorHistory = [];
    public IReadOnlyCollection<RoomFloorHistory> FloorHistory => _floorHistory.AsReadOnly();

    private readonly List<RoomBuildingHistory> _buildingHistory = [];
    public IReadOnlyCollection<RoomBuildingHistory> BuildingHistory => _buildingHistory.AsReadOnly();

    private readonly List<RoomFunctionHistory> _functionHistory = [];
    public IReadOnlyCollection<RoomFunctionHistory> FunctionHistory => _functionHistory.AsReadOnly();

    private readonly List<RoomExposureHistory> _exposureHistory = [];
    public IReadOnlyCollection<RoomExposureHistory> ExposureHistory => _exposureHistory.AsReadOnly();

    private readonly List<RoomGeometryHistory> _geometryHistory = [];
    public IReadOnlyCollection<RoomGeometryHistory> GeometryHistory => _geometryHistory.AsReadOnly();

    private readonly List<RoomVentilationHistory> _ventilationHistory = [];
    public IReadOnlyCollection<RoomVentilationHistory> VentilationHistory => _ventilationHistory.AsReadOnly();

    private readonly List<RoomPollutionSourceHistory> _pollutionSourceHistory = [];
    public IReadOnlyCollection<RoomPollutionSourceHistory> PollutionSourceHistory => _pollutionSourceHistory.AsReadOnly();

    public static Room Register(
        UriSlug slug,
        Guid buildingId,
        Guid createdBy,
        string name,
        FloorNumber floor,
        string? functionCode,
        string? exposureCode,
        double? areaM2,
        double? ceilingHeightM,
        string? ventilationType,
        IReadOnlyCollection<string> pollutionSources,
        DateTime now)
    {
        var id = Guid.NewGuid();
        var validity = Validity.OpenFrom(now);

        var room = new Room
        {
            Id = id,
            UriSlug = slug.Value,
            BuildingId = buildingId,
            CreatedAt = now,
            CreatedBy = createdBy,
        };

        room._nameHistory.Add(new RoomNameHistory(id, validity, name, createdBy, now));
        room._floorHistory.Add(new RoomFloorHistory(id, validity, floor.Value, createdBy, now));
        room._buildingHistory.Add(new RoomBuildingHistory(id, validity, buildingId, createdBy, now));
        room._functionHistory.Add(new RoomFunctionHistory(id, validity, functionCode, createdBy, now));
        room._exposureHistory.Add(new RoomExposureHistory(id, validity, exposureCode, createdBy, now));
        room._geometryHistory.Add(new RoomGeometryHistory(id, validity, areaM2, ceilingHeightM, createdBy, now));
        room._ventilationHistory.Add(new RoomVentilationHistory(id, validity, ventilationType, createdBy, now));

        foreach (var source in pollutionSources)
        {
            room._pollutionSourceHistory.Add(new RoomPollutionSourceHistory(id, source, validity, createdBy, now));
        }

        return room;
    }

    public void ChangeName(string newName, DateTime validFrom, Guid recordedBy)
    {
        var current = _nameHistory.Single(h => h.Validity.UpperBoundInfinite);

        if (validFrom <= current.Validity.LowerBound)
            throw new DomainException("ValidFrom must be after the current open range's start");

        current.Close(validFrom);
        _nameHistory.Add(new RoomNameHistory(Id, Validity.OpenFrom(validFrom), newName, recordedBy, validFrom));
    }

    public void ChangeFloor(FloorNumber newFloor, DateTime validFrom, Guid recordedBy)
    {
        var current = _floorHistory.Single(h => h.Validity.UpperBoundInfinite);

        if (validFrom <= current.Validity.LowerBound)
            throw new DomainException("ValidFrom must be after the current open range's start");

        current.Close(validFrom);
        _floorHistory.Add(new RoomFloorHistory(Id, Validity.OpenFrom(validFrom), newFloor.Value, recordedBy, validFrom));
    }

    public void ChangeFunction(string? newFunctionCode, DateTime validFrom, Guid recordedBy)
    {
        var current = _functionHistory.Single(h => h.Validity.UpperBoundInfinite);

        if (validFrom <= current.Validity.LowerBound)
            throw new DomainException("ValidFrom must be after the current open range's start");

        current.Close(validFrom);
        _functionHistory.Add(new RoomFunctionHistory(Id, Validity.OpenFrom(validFrom), newFunctionCode, recordedBy, validFrom));
    }

    public void ChangeExposure(string? newExposureCode, DateTime validFrom, Guid recordedBy)
    {
        var current = _exposureHistory.Single(h => h.Validity.UpperBoundInfinite);

        if (validFrom <= current.Validity.LowerBound)
            throw new DomainException("ValidFrom must be after the current open range's start");

        current.Close(validFrom);
        _exposureHistory.Add(new RoomExposureHistory(Id, Validity.OpenFrom(validFrom), newExposureCode, recordedBy, validFrom));
    }

    public void ChangeGeometry(double? areaM2, double? ceilingHeightM, DateTime validFrom, Guid recordedBy)
    {
        var current = _geometryHistory.Single(h => h.Validity.UpperBoundInfinite);

        if (validFrom <= current.Validity.LowerBound)
            throw new DomainException("ValidFrom must be after the current open range's start");

        current.Close(validFrom);
        _geometryHistory.Add(new RoomGeometryHistory(Id, Validity.OpenFrom(validFrom), areaM2, ceilingHeightM, recordedBy, validFrom));
    }

    public void ChangeVentilation(string? newVentilationType, DateTime validFrom, Guid recordedBy)
    {
        var current = _ventilationHistory.Single(h => h.Validity.UpperBoundInfinite);

        if (validFrom <= current.Validity.LowerBound)
            throw new DomainException("ValidFrom must be after the current open range's start");

        current.Close(validFrom);
        _ventilationHistory.Add(new RoomVentilationHistory(Id, Validity.OpenFrom(validFrom), newVentilationType, recordedBy, validFrom));
    }

    public void AddPollutionSource(string sourceCode, DateTime validFrom)
    {
        var validity = Validity.OpenFrom(validFrom);
        _pollutionSourceHistory.Add(new RoomPollutionSourceHistory(Id, sourceCode, validity, CreatedBy, validFrom));
    }

    public void RemovePollutionSource(string sourceCode, DateTime validTo)
    {
        var current = _pollutionSourceHistory
            .Where(h => h.SourceCode == sourceCode && h.Validity.UpperBoundInfinite)
            .SingleOrDefault();

        if (current is null)
            throw new PollutionSourceNotFoundException(sourceCode);

        current.Close(validTo);
    }

    public RoomSnapshot SnapshotAt(DateTime asOf) =>
        new(
            Id: Id,
            UriSlug: UriSlug,
            BuildingId: BuildingId,
            CreatedAt: CreatedAt,
            CreatedBy: CreatedBy,
            Name: _nameHistory.Single(h => asOf >= h.Validity.LowerBound && (h.Validity.UpperBoundInfinite || asOf < h.Validity.UpperBound)).Name,
            Floor: _floorHistory.Single(h => asOf >= h.Validity.LowerBound && (h.Validity.UpperBoundInfinite || asOf < h.Validity.UpperBound)).Floor,
            FunctionCode: _functionHistory.Single(h => asOf >= h.Validity.LowerBound && (h.Validity.UpperBoundInfinite || asOf < h.Validity.UpperBound)).FunctionCode,
            ExposureCode: _exposureHistory.Single(h => asOf >= h.Validity.LowerBound && (h.Validity.UpperBoundInfinite || asOf < h.Validity.UpperBound)).ExposureCode,
            AreaM2: _geometryHistory.Single(h => asOf >= h.Validity.LowerBound && (h.Validity.UpperBoundInfinite || asOf < h.Validity.UpperBound)).AreaM2,
            CeilingHeightM: _geometryHistory.Single(h => asOf >= h.Validity.LowerBound && (h.Validity.UpperBoundInfinite || asOf < h.Validity.UpperBound)).CeilingHeightM,
            VentilationType: _ventilationHistory.Single(h => asOf >= h.Validity.LowerBound && (h.Validity.UpperBoundInfinite || asOf < h.Validity.UpperBound)).VentilationType,
            PollutionSources: _pollutionSourceHistory
                .Where(h => asOf >= h.Validity.LowerBound && (h.Validity.UpperBoundInfinite || asOf < h.Validity.UpperBound))
                .Select(h => h.SourceCode)
                .ToList(),
            AsOf: asOf);
}

public sealed record RoomSnapshot(
    Guid Id,
    string UriSlug,
    Guid BuildingId,
    DateTime CreatedAt,
    Guid CreatedBy,
    string Name,
    byte Floor,
    string? FunctionCode,
    string? ExposureCode,
    double? AreaM2,
    double? CeilingHeightM,
    string? VentilationType,
    IReadOnlyCollection<string> PollutionSources,
    DateTime AsOf);
