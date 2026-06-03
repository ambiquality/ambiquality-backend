using Ambiquality.Export.Worker.Persistence;
using Ambiquality.Export.Worker.Serialization;

namespace Ambiquality.Export.Worker.Exporting;

/// <summary>
/// One export representation (CSV or JSON-LD): its media type, the key/entry naming,
/// and the streaming serializer that writes the rows. The serializer receives the
/// feature-of-interest resolver (used by JSON-LD, ignored by CSV) and returns the
/// number of records written.
/// </summary>
public sealed record ExportFormat(
    string MediaType,
    string KeySuffix,
    string ZipEntryName,
    Func<IAsyncEnumerable<MeasurementRow>, Stream, FeatureOfInterestResolver, CancellationToken, Task<long>> Serialize);
