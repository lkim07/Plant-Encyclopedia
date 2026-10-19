using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.ExternalData.Images;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.ExternalData.INaturalist;

/// <summary>
/// Photos from iNaturalist (ARCHITECTURE §21). Two GETs: a taxon search to find the active Plantae taxon
/// whose name is exactly the scientific name, then its details for the curator-ordered taxon photos.
/// HTTPS to api.inaturalist.org only. Only photos of that exact taxon, hosted on iNaturalist's open-data
/// bucket, are offered; their licences are checked by <see cref="ExternalImagePolicy"/>.
/// </summary>
internal sealed class INaturalistPhotoSource(HttpClient http, ILogger<INaturalistPhotoSource> logger) : IPlantImageSource
{
    public const string ProviderName = "inaturalist";
    public static readonly Uri BaseAddress = new("https://api.inaturalist.org/");
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    // Openly licensed photos are served from here; "all rights reserved" ones from static.inaturalist.org.
    private const string PhotoHost = "inaturalist-open-data.s3.amazonaws.com";

    private static readonly HashSet<string> Ranks = ["species", "hybrid", "subspecies", "variety"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public PlantImageKind Kind => PlantImageKind.Photo;

    public async Task<IReadOnlyList<ExternalImageCandidate>> GetCandidatesAsync(string scientificName, CancellationToken cancellationToken)
    {
        var search = await ExternalJsonClient.GetAsync<INaturalistTaxaResponse>(
            http, $"v1/taxa?q={Uri.EscapeDataString(scientificName)}&is_active=true&per_page=10",
            JsonOptions, "iNaturalist taxon search", logger, cancellationToken);

        var taxon = search?.Results?.FirstOrDefault(t =>
            t is not null
            && t.Name == scientificName
            && t.IsActive == true
            && t.IconicTaxonName == "Plantae"
            && t.Rank is not null && Ranks.Contains(t.Rank));
        if (taxon is null) return [];

        var details = await ExternalJsonClient.GetAsync<INaturalistTaxaResponse>(
            http, $"v1/taxa/{taxon.Id}", JsonOptions, "iNaturalist taxon details", logger, cancellationToken);
        var photos = details?.Results?.FirstOrDefault(t => t?.Id == taxon.Id)?.TaxonPhotos ?? [];

        return photos
            .Where(tp => tp?.TaxonId == taxon.Id && tp.Photo is not null)
            .Select(tp => ToCandidate(tp!.Photo!))
            .OfType<ExternalImageCandidate>()
            .ToList();
    }

    private static ExternalImageCandidate? ToCandidate(INaturalistPhoto photo)
    {
        if (!Uri.TryCreate(photo.OriginalUrl, UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps
            || url.Host != PhotoHost
            || photo.OriginalDimensions is null)
        {
            return null;
        }

        return new ExternalImageCandidate(
            Provider: ProviderName,
            SourceId: photo.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Kind: PlantImageKind.Photo,
            DownloadUrl: url,
            Width: photo.OriginalDimensions.Width,
            Height: photo.OriginalDimensions.Height,
            MimeType: MimeTypeFromExtension(url),
            LicenseCode: photo.LicenseCode,
            ProviderRequiresAttribution: false,
            Creator: photo.AttributionName,
            // The API gives no photo page URL; the pattern is not verified yet, so none is invented.
            SourcePageUrl: null);
    }

    // The API reports no file type; the URL ends in .jpg, .jpeg, .JPG or .png. The download step checks the bytes.
    private static string? MimeTypeFromExtension(Uri url) =>
        Path.GetExtension(url.AbsolutePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };
}
