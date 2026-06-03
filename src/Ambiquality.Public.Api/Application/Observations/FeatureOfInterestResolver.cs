using Ambiquality.Public.Api.Infrastructure.Catalog;

namespace Ambiquality.Public.Api.Application.Observations;

/// <summary>
/// Resolves the room a sensor occupied at a given instant from its placement history,
/// so an observation's <c>sosa:hasFeatureOfInterest</c> reflects where the measurement
/// was actually taken — not merely where the sensor sits now. Placement periods are
/// half-open <c>[ValidFrom, ValidTo)</c>; an instant before a sensor's first placement
/// (or for an unknown sensor) resolves to null and the feature is simply omitted.
/// </summary>
public sealed class FeatureOfInterestResolver
{
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<SensorPlacement>> _bySensor;

    public FeatureOfInterestResolver(IReadOnlyList<SensorPlacement> placements) =>
        _bySensor = placements
            .GroupBy(p => p.SensorId)
            .ToDictionary(g => g.Key, IReadOnlyList<SensorPlacement> (g) => g.ToList());

    /// <summary>The room id the sensor was placed in at <paramref name="at"/>, or null if none covers it.</summary>
    public Guid? ResolveRoomId(Guid sensorId, DateTime at)
    {
        if (!_bySensor.TryGetValue(sensorId, out var periods))
            return null;

        foreach (var p in periods)
            if (at >= p.ValidFrom && (p.ValidTo is not { } to || at < to))
                return p.RoomId;

        return null;
    }
}
