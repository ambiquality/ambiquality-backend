namespace Ambiquality.Export.Worker;

/// <summary>
/// Configuration for the monthly export worker, bound from the <c>Export</c> section.
/// <see cref="StorageType"/> selects the <c>IExportStorage</c> implementation
/// ("S3" or "FileSystem").
/// </summary>
public sealed class ExportOptions
{
    public const string SectionName = "Export";

    public string StorageType { get; set; } = "FileSystem";

    /// <summary>
    /// Absolute public API root used to anchor JSON-LD <c>@id</c> / <c>@context</c>
    /// IRIs in the exported graph, e.g. <c>https://data.ambiquality.org</c>. The
    /// versioned <c>/v1</c> segment is appended automatically.
    /// </summary>
    public string BaseIri { get; set; } = "https://data.ambiquality.org";

    public S3Options S3 { get; set; } = new();

    public FileSystemOptions FileSystem { get; set; } = new();
}

public sealed class S3Options
{
    public string? ServiceUrl { get; set; }
    public string BucketName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Base URL the public download links are built from (bucket-qualified).</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}

public sealed class FileSystemOptions
{
    public string BasePath { get; set; } = "/exports";

    /// <summary>Base URL the public download links are built from when storing on disk.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
