using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// Aggregate root for a physical building. Holds an immutable identity row
/// plus one history collection per mutable attribute; every state transition
/// closes the current open row and appends a new one in a single behavior
/// method, so the half-open <c>[lower, upper)</c> ranges stay contiguous and
/// non-overlapping. The database-level GiST exclusion constraints back this
/// up as a safety net.
/// </summary>
public sealed class Building
{
    private readonly List<BuildingNameHistory> _nameHistory = [];
    private readonly List<BuildingAddressHistory> _addressHistory = [];
    private readonly List<BuildingTypeHistory> _typeHistory = [];
    private readonly List<BuildingLocationHistory> _locationHistory = [];
    private readonly List<BuildingYearsHistory> _yearsHistory = [];

    // Parameterless constructor for EF Core materialization.
    private Building()
    {
        UriSlug = null!;
    }

    private Building(Guid id, string uriSlug, Guid ownerId, Guid createdBy, DateTime createdAt)
    {
        Id = id;
        UriSlug = uriSlug;
        OwnerId = ownerId;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Public-facing kebab-case identifier; unique across buildings.</summary>
    public string UriSlug { get; private set; }

    /// <summary>Immutable per locked decision 12; reassignment is out of scope.</summary>
    public Guid OwnerId { get; private set; }

    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<BuildingNameHistory> NameHistory => _nameHistory.AsReadOnly();
    public IReadOnlyCollection<BuildingAddressHistory> AddressHistory => _addressHistory.AsReadOnly();
    public IReadOnlyCollection<BuildingTypeHistory> TypeHistory => _typeHistory.AsReadOnly();
    public IReadOnlyCollection<BuildingLocationHistory> LocationHistory => _locationHistory.AsReadOnly();
    public IReadOnlyCollection<BuildingYearsHistory> YearsHistory => _yearsHistory.AsReadOnly();

    /// <summary>
    /// Creates a new building with one open-ended history row per attribute.
    /// The optional <paramref name="coordinates"/> is null when the operator
    /// only knows the building's municipality; the row is still seeded so the
    /// <paramref name="anonymization"/> level is queryable.
    /// </summary>
    public static Building Register(
        UriSlug slug,
        Guid ownerId,
        Guid createdBy,
        string name,
        Address address,
        string buildingTypeCode,
        Coordinates? coordinates,
        AnonymizationLevel anonymization,
        short? yearBuilt,
        short? yearRenovated,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(anonymization);
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Building name cannot be empty.");
        if (string.IsNullOrWhiteSpace(buildingTypeCode))
            throw new DomainException("Building type code cannot be empty.");
        EnsureUtc(now, nameof(now));

        var building = new Building(Guid.NewGuid(), slug.Value, ownerId, createdBy, now);

        var open = Validity.OpenFrom(now);
        building._nameHistory.Add(new BuildingNameHistory(name, open, now, createdBy));
        building._addressHistory.Add(new BuildingAddressHistory(address, open, now, createdBy));
        building._typeHistory.Add(new BuildingTypeHistory(buildingTypeCode, open, now, createdBy));
        building._locationHistory.Add(new BuildingLocationHistory(coordinates, anonymization, open, now, createdBy));
        building._yearsHistory.Add(new BuildingYearsHistory(yearBuilt, yearRenovated, open, now, createdBy));

        return building;
    }

    public void ChangeName(string newName, DateTime validFrom, Guid recordedBy)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("Building name cannot be empty.");

        var current = OpenRow(_nameHistory, "name");
        EnsureAdvancing(current.Validity.LowerBound, validFrom, "name");

        current.Close(validFrom);
        _nameHistory.Add(new BuildingNameHistory(
            newName, Validity.OpenFrom(validFrom), validFrom, recordedBy));
    }

    public void ChangeAddress(Address newAddress, DateTime validFrom, Guid recordedBy)
    {
        ArgumentNullException.ThrowIfNull(newAddress);

        var current = OpenRow(_addressHistory, "address");
        EnsureAdvancing(current.Validity.LowerBound, validFrom, "address");

        current.Close(validFrom);
        _addressHistory.Add(new BuildingAddressHistory(
            newAddress, Validity.OpenFrom(validFrom), validFrom, recordedBy));
    }

    public void ChangeType(string newTypeCode, DateTime validFrom, Guid recordedBy)
    {
        if (string.IsNullOrWhiteSpace(newTypeCode))
            throw new DomainException("Building type code cannot be empty.");

        var current = OpenRow(_typeHistory, "type");
        EnsureAdvancing(current.Validity.LowerBound, validFrom, "type");

        current.Close(validFrom);
        _typeHistory.Add(new BuildingTypeHistory(
            newTypeCode, Validity.OpenFrom(validFrom), validFrom, recordedBy));
    }

    public void ChangeLocation(
        Coordinates? newCoordinates,
        AnonymizationLevel anonymization,
        DateTime validFrom,
        Guid recordedBy)
    {
        ArgumentNullException.ThrowIfNull(anonymization);

        var current = OpenRow(_locationHistory, "location");
        EnsureAdvancing(current.Validity.LowerBound, validFrom, "location");

        current.Close(validFrom);
        _locationHistory.Add(new BuildingLocationHistory(
            newCoordinates, anonymization, Validity.OpenFrom(validFrom), validFrom, recordedBy));
    }

    public void ChangeYears(short? yearBuilt, short? yearRenovated, DateTime validFrom, Guid recordedBy)
    {
        var current = OpenRow(_yearsHistory, "years");
        EnsureAdvancing(current.Validity.LowerBound, validFrom, "years");

        current.Close(validFrom);
        _yearsHistory.Add(new BuildingYearsHistory(
            yearBuilt, yearRenovated, Validity.OpenFrom(validFrom), validFrom, recordedBy));
    }

    /// <summary>
    /// Rebuilds the projection of this building at <paramref name="asOf"/> by
    /// picking, for every attribute, the single history row whose validity
    /// range contains the instant.
    /// </summary>
    public BuildingSnapshot SnapshotAt(DateTime asOf)
    {
        EnsureUtc(asOf, nameof(asOf));
        if (asOf < CreatedAt)
            throw new DomainException(
                "Cannot reconstruct a building snapshot before its creation time.");

        var name = RowAt(_nameHistory, asOf, "name");
        var address = RowAt(_addressHistory, asOf, "address");
        var type = RowAt(_typeHistory, asOf, "type");
        var location = RowAt(_locationHistory, asOf, "location");
        var years = RowAt(_yearsHistory, asOf, "years");

        return new BuildingSnapshot(
            Id,
            UriSlug,
            OwnerId,
            name.Name,
            address.Address,
            type.BuildingTypeCode,
            location.Coordinates,
            location.Anonymization,
            years.YearBuilt,
            years.YearRenovated,
            asOf);
    }

    // ---- invariant helpers --------------------------------------------------

    private static T OpenRow<T>(IReadOnlyList<T> rows, string attributeName)
        where T : class
    {
        // The single open row is the one whose validity is upper-infinite.
        var open = rows.FirstOrDefault(r => GetValidity(r).UpperBoundInfinite);
        if (open is null)
            throw new MissingOpenAttributeHistoryException(attributeName);
        return open;
    }

    private static T RowAt<T>(IReadOnlyList<T> rows, DateTime asOf, string attributeName)
        where T : class
    {
        var hit = rows.FirstOrDefault(r => Contains(GetValidity(r), asOf));
        if (hit is null)
            throw new DomainException(
                $"No {attributeName} history row covers the instant {asOf:O}.");
        return hit;
    }

    private static NpgsqlTypes.NpgsqlRange<DateTime> GetValidity<T>(T row) where T : class
        => row switch
        {
            BuildingNameHistory n => n.Validity,
            BuildingAddressHistory a => a.Validity,
            BuildingTypeHistory t => t.Validity,
            BuildingLocationHistory l => l.Validity,
            BuildingYearsHistory y => y.Validity,
            _ => throw new InvalidOperationException(
                $"Unknown history row type {typeof(T).Name}.")
        };

    private static bool Contains(NpgsqlTypes.NpgsqlRange<DateTime> range, DateTime instant)
    {
        // Lower bound: inclusive by construction.
        if (instant < range.LowerBound)
            return false;
        // Upper bound: half-open; infinite ranges always contain anything >= lower.
        if (range.UpperBoundInfinite)
            return true;
        return instant < range.UpperBound;
    }

    private static void EnsureAdvancing(DateTime currentLower, DateTime validFrom, string attributeName)
    {
        EnsureUtc(validFrom, nameof(validFrom));
        if (validFrom <= currentLower)
            throw new DomainException(
                $"New {attributeName} valid-from {validFrom:O} must be strictly after the current value's start {currentLower:O}.");
    }

    private static void EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new DomainException(
                $"Timestamps must be UTC (parameter '{paramName}').");
    }
}

/// <summary>
/// Raised when an aggregate is asked to transition an attribute that has no
/// open history row — a corruption symptom that should be impossible if all
/// state changes go through the aggregate's behavior methods.
/// </summary>
public sealed class MissingOpenAttributeHistoryException : DomainException
{
    public MissingOpenAttributeHistoryException(string attributeName)
        : base($"No open history row found for attribute '{attributeName}'.") { }
}
