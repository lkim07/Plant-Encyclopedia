using System.Text.RegularExpressions;

namespace PlantEncyclopedia.Infrastructure.ExternalData.Images;

/// <summary>
/// Configuration section ExternalPlantData:Images. Image import is off unless Enabled is true, and the API always
/// turns it off in the Testing environment. Files are stored in a local folder (served from <see cref="RequestPath"/>)
/// or in Cloudflare R2 (served from <see cref="R2Options.PublicBaseUrl"/>).
/// </summary>
public sealed partial class PlantImageOptions
{
    public const string SectionName = "ExternalPlantData:Images";

    /// <summary>The URL prefix locally stored image files are served under.</summary>
    public const string RequestPath = "/media";

    public bool Enabled { get; set; }

    /// <summary>Where new image files are written: <see cref="ImageStorageKind.Local"/> (default) or <see cref="ImageStorageKind.R2"/>.</summary>
    public ImageStorageKind Storage { get; set; } = ImageStorageKind.Local;

    /// <summary>
    /// The local folder images are written to and served from. Relative paths are resolved against the API's
    /// content root by the API before the options reach Infrastructure.
    /// </summary>
    public string StoragePath { get; set; } = "App_Data/plant-images";

    /// <summary>Cloudflare R2 settings; used when <see cref="Storage"/> is R2. The keys belong in User Secrets.</summary>
    public R2Options R2 { get; set; } = new();

    /// <summary>At startup, queue species-level plants that have no usable photo (at most <see cref="BackfillLimit"/>).</summary>
    public bool BackfillOnStartup { get; set; }

    public int BackfillLimit { get; set; } = 50;

    /// <summary>Throws when image import is enabled with unsafe or incomplete settings.</summary>
    public void Validate()
    {
        if (!Enabled) return;

        if (!Path.IsPathFullyQualified(StoragePath))
        {
            throw new InvalidOperationException($"{SectionName}:StoragePath must be resolved to an absolute path.");
        }

        if (BackfillLimit is < 0 or > 1_000)
        {
            throw new InvalidOperationException($"{SectionName}:BackfillLimit must be between 0 and 1000.");
        }

        if (Storage == ImageStorageKind.R2)
        {
            R2.Validate();
        }
    }
}

public enum ImageStorageKind
{
    Local,
    R2
}

/// <summary>Cloudflare R2 bucket access. Written through the S3-compatible API; read by browsers at <see cref="PublicBaseUrl"/>.</summary>
public sealed partial class R2Options
{
    private const string Section = PlantImageOptions.SectionName + ":R2";

    /// <summary>The Cloudflare account ID (32 hex characters); the S3 endpoint is https://{AccountId}.r2.cloudflarestorage.com.</summary>
    public string? AccountId { get; set; }

    /// <summary>An R2 API token's access key ID. User Secrets only.</summary>
    public string? AccessKeyId { get; set; }

    /// <summary>The same token's secret access key. User Secrets only; never logged.</summary>
    public string? SecretAccessKey { get; set; }

    public string Bucket { get; set; } = "plant-encyclopedia-images";

    /// <summary>The bucket's public address, e.g. https://pub-….r2.dev or a custom domain. Image URLs start with it.</summary>
    public string? PublicBaseUrl { get; set; }

    public Uri Endpoint => new($"https://{AccountId}.r2.cloudflarestorage.com");

    public void Validate()
    {
        if (AccountId is null || !AccountIdPattern().IsMatch(AccountId))
        {
            throw new InvalidOperationException($"{Section}:AccountId must be a 32-character Cloudflare account ID.");
        }

        if (string.IsNullOrWhiteSpace(AccessKeyId) || string.IsNullOrWhiteSpace(SecretAccessKey))
        {
            throw new InvalidOperationException($"{Section}:AccessKeyId and SecretAccessKey must be set (User Secrets).");
        }

        if (!BucketPattern().IsMatch(Bucket))
        {
            throw new InvalidOperationException($"{Section}:Bucket is not a valid bucket name.");
        }

        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var publicBase)
            || publicBase.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(publicBase.UserInfo)
            || !string.IsNullOrEmpty(publicBase.Query))
        {
            throw new InvalidOperationException($"{Section}:PublicBaseUrl must be an absolute https URL without credentials or query.");
        }
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex AccountIdPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$")]
    private static partial Regex BucketPattern();
}
