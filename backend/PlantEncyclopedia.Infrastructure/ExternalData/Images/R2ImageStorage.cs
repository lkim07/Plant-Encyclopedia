using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace PlantEncyclopedia.Infrastructure.ExternalData.Images;

/// <summary>
/// Stores image files in a Cloudflare R2 bucket through its S3-compatible API (Phase 11d-3) and returns their public
/// URLs under <see cref="R2Options.PublicBaseUrl"/>. File names contain a new GUID per image, so they never change and
/// are cached for a year. The keys come from User Secrets and are never logged.
/// </summary>
internal sealed class R2ImageStorage : IImageStorage, IDisposable
{
    public const string CacheControl = "public, max-age=31536000, immutable";

    private readonly R2Options _options;
    private readonly AmazonS3Client _client;

    /// <param name="httpClientFactory">Tests pass a factory whose HttpClient answers in memory; production uses the SDK's own.</param>
    public R2ImageStorage(PlantImageOptions options, HttpClientFactory? httpClientFactory = null)
    {
        _options = options.R2;
        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint.AbsoluteUri,
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            // R2 does not accept every checksum the SDK adds by default; send and check them only when required.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            MaxErrorRetry = 2,
            Timeout = TimeSpan.FromSeconds(30)
        };
        if (httpClientFactory is not null)
        {
            config.HttpClientFactory = httpClientFactory;
        }

        _client = new AmazonS3Client(new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey), config);
    }

    public async Task<string> SaveAsync(string relativePath, byte[] content, CancellationToken cancellationToken)
    {
        // Keys are built from GUIDs and fixed names; refuse anything else rather than trust it.
        if (relativePath.Contains("..", StringComparison.Ordinal) || relativePath.StartsWith('/') || relativePath.Contains('\\'))
        {
            throw new ArgumentException("The image key must be a plain relative path.", nameof(relativePath));
        }

        using var stream = new MemoryStream(content, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = relativePath,
            InputStream = stream,
            ContentType = "image/webp",
            // R2 does not support streaming (chunked) payload signing.
            DisablePayloadSigning = true
        };
        request.Headers.CacheControl = CacheControl;

        await _client.PutObjectAsync(request, cancellationToken);
        return $"{_options.PublicBaseUrl!.TrimEnd('/')}/{relativePath}";
    }

    /// <summary>Deletes one object; used to clean up after the connection check.</summary>
    internal Task DeleteAsync(string relativePath, CancellationToken cancellationToken) =>
        _client.DeleteObjectAsync(_options.Bucket, relativePath, cancellationToken);

    public void Dispose() => _client.Dispose();
}
