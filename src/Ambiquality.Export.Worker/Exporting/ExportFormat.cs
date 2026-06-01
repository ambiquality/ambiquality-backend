using Ambiquality.Export.Worker.Persistence;

namespace Ambiquality.Export.Worker.Exporting;

/// <summary>
/// One export representation (CSV or JSON-LD): its media type, the key/entry naming,
/// and the streaming serializer that writes the rows. The serializer returns the
/// number of records written.
/// </summary>
public sealed record ExportFormat(
    string MediaType,
    string KeySuffix,
    string ZipEntryName,
    Func<IAsyncEnumerable<MeasurementRow>, Stream, CancellationToken, Task<long>> Serialize);
