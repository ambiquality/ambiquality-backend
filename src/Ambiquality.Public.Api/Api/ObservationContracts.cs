using Ambiquality.Core.Domain.Measurements;
using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Plain-JSON projection of a single measurement as an SSN/SOSA observation.
/// <see cref="ObservedPropertyIri"/> is the substance-specific observed property
/// (distinguishes PM2.5 from PM10, VOC from CO₂); <see cref="QuantityKindUri"/> is
/// the coarser QUDT <em>dimensional</em> kind shared across such parameters.
/// </summary>
public sealed record ObservationResponse(
    Guid Id,
    string Iri,
    Guid SensorId,
    string ParameterCode,
    double Value,
    string? Unit,
    string ObservedPropertyIri,
    string? QuantityKindUri,
    string? UnitUri,
    DateTime ObservedAt,
    DateTime ReceivedAt,
    bool IsInvalid,
    string License)
{
    public static ObservationResponse From(Measurement measurement, IriBuilder iri)
    {
        var qudt = QudtVocabulary.TryResolve(measurement.ParameterCode);
        return new ObservationResponse(
            measurement.Id,
            iri.Observation(measurement.Id),
            measurement.SensorId,
            measurement.ParameterCode,
            measurement.Value,
            measurement.Unit,
            iri.Property(measurement.ParameterCode),
            qudt?.QuantityKindUri,
            qudt?.UnitUri,
            measurement.ObservedAt,
            measurement.ReceivedAt,
            measurement.IsInvalid,
            Constants.LicenseIri);
    }
}

/// <summary>
/// A page of plain-JSON observations. <see cref="Next"/> is the absolute IRI of the
/// next page (opaque cursor embedded) or <c>null</c> when the result set is exhausted;
/// <see cref="NextCursor"/> exposes the raw cursor for clients that prefer to drive paging themselves.
/// </summary>
public sealed record ObservationPage(
    IReadOnlyList<ObservationResponse> Items,
    string? NextCursor,
    string? Next,
    string License);

/// <summary>
/// Projects observations into JSON-LD using the terms defined by the served
/// <c>measurements.jsonld</c> context. A single resource carries <c>@context</c>;
/// collection members do not (the wrapping <c>@graph</c> object carries it once).
/// </summary>
public static class ObservationJsonLd
{
    public static IReadOnlyDictionary<string, object?> ToResource(
        ObservationResponse o, IriBuilder iri, bool includeContext)
    {
        var doc = new Dictionary<string, object?>();

        if (includeContext)
            doc["@context"] = iri.Context();

        doc["@id"] = o.Iri;
        doc["@type"] = "sosa:Observation";

        doc["sosa:observedProperty"] = new Dictionary<string, object?> { ["@id"] = o.ObservedPropertyIri };

        if (o.QuantityKindUri is not null)
            doc["qudt:hasQuantityKind"] = new Dictionary<string, object?> { ["@id"] = o.QuantityKindUri };

        doc["sosa:madeBySensor"] = new Dictionary<string, object?> { ["@id"] = iri.Sensor(o.SensorId) };
        doc["sosa:hasSimpleResult"] = o.Value;

        if (o.UnitUri is not null)
            doc["qudt:unit"] = new Dictionary<string, object?> { ["@id"] = o.UnitUri };

        doc["sosa:resultTime"] = o.ObservedAt;
        doc["ambiq:receivedTime"] = o.ReceivedAt;
        doc["ambiq:isInvalid"] = o.IsInvalid;
        doc["license"] = o.License;

        return doc;
    }

    /// <summary>
    /// Wraps a page of observations in a JSON-LD document: a single <c>@context</c>,
    /// the members under <c>@graph</c>, the license, and an optional next-page link.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> ToGraph(
        IEnumerable<ObservationResponse> items, IriBuilder iri, string? nextIri)
    {
        var doc = new Dictionary<string, object?>
        {
            ["@context"] = iri.Context(),
            ["@graph"] = items.Select(o => ToResource(o, iri, includeContext: false)).ToList(),
            ["license"] = Constants.LicenseIri
        };

        if (nextIri is not null)
            doc["ambiq:next"] = new Dictionary<string, object?> { ["@id"] = nextIri };

        return doc;
    }
}
