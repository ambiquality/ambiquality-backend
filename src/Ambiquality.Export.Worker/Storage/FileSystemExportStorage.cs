using Microsoft.Extensions.Options;

namespace Ambiquality.Export.Worker.Storage;

/// <summary>
/// Writes export objects to a local directory tree mirroring the object key, for
/// dev and tests. The returned URL is the configured <c>PublicBaseUrl</c> plus the
/// key when set, otherwise a <c>file://</c> URI for the written path.
/// </summary>
public sealed class FileSystemExportStorage(IOptions<ExportOptions> options) : IExportStorage
{
    private readonly FileSystemOptions _options = options.Value.FileSystem;

    public async Task<Uri> UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = Path.Combine(_options.BasePath, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using (var file = File.Create(path))
            await content.CopyToAsync(file, ct);

        return string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? new Uri(Path.GetFullPath(path))
            : new Uri($"{_options.PublicBaseUrl.TrimEnd('/')}/{key}");
    }
}
