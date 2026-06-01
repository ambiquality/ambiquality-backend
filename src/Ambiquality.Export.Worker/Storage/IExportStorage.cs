namespace Ambiquality.Export.Worker.Storage;

/// <summary>
/// Sink for a finished export object. Implementations upload the (already compressed)
/// content under a stable key and return the public download URL.
/// </summary>
public interface IExportStorage
{
    Task<Uri> UploadAsync(string key, Stream content, string contentType, CancellationToken ct);
}
