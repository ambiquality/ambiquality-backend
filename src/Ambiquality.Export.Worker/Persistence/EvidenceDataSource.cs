using Npgsql;

namespace Ambiquality.Export.Worker.Persistence;

/// <summary>
/// Typed wrapper around the read-only <c>evidence</c> Npgsql data source, so DI can tell
/// it apart from the <c>ieq</c> <see cref="NpgsqlDataSource"/> the export path uses.
/// </summary>
public sealed class EvidenceDataSource(NpgsqlDataSource source)
{
    public NpgsqlDataSource Source { get; } = source;
}
