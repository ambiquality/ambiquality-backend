using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Ambiquality.Export.Worker.Storage;

/// <summary>
/// Uploads export objects to an S3-compatible store (Hetzner Object Storage) via a
/// configurable <c>ServiceURL</c>. The public download URL is built from the
/// configured <c>PublicBaseUrl</c> plus the object key rather than the S3 endpoint,
/// so it survives behind a CDN or custom domain.
/// </summary>
public sealed class S3ExportStorage : IExportStorage
{
    private readonly IAmazonS3 _client;
    private readonly S3Options _options;

    public S3ExportStorage(IOptions<ExportOptions> options)
    {
        _options = options.Value.S3;
        var config = new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            ForcePathStyle = true
        };
        _client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
    }

    public async Task<Uri> UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };
        await _client.PutObjectAsync(request, ct);

        return new Uri($"{_options.PublicBaseUrl.TrimEnd('/')}/{key}");
    }
}
