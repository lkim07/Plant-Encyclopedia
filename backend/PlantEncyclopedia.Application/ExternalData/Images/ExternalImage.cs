using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Application.ExternalData.Images;

/// <summary>
/// An image a source offers for a plant, in provider-neutral terms. Untrusted until
/// <see cref="ExternalImagePolicy"/> has accepted it. Nothing has been downloaded yet.
/// </summary>
/// <param name="Provider">Stable provider name, e.g. "inaturalist" or "wikimedia-commons".</param>
/// <param name="SourceId">The provider's ID for the image (iNaturalist photo ID, Commons file title).</param>
/// <param name="DownloadUrl">Where the image file can be fetched (HTTPS, a host the source trusts).</param>
/// <param name="Width">Width of the file at <paramref name="DownloadUrl"/>, in pixels.</param>
/// <param name="Height">Height of the file at <paramref name="DownloadUrl"/>, in pixels.</param>
/// <param name="MimeType">The file type the provider reports, e.g. "image/jpeg".</param>
/// <param name="LicenseCode">The provider's licence code, e.g. "cc-by-sa", "cc-by-sa-3.0", "cc0", "pd"; null when none is given.</param>
/// <param name="ProviderRequiresAttribution">The provider says credit is required, whatever the licence code says.</param>
/// <param name="Creator">The author's name as plain text, when known.</param>
/// <param name="SourcePageUrl">The provider's page for the image, when the provider gives one.</param>
public sealed record ExternalImageCandidate(
    string Provider,
    string SourceId,
    PlantImageKind Kind,
    Uri DownloadUrl,
    int Width,
    int Height,
    string? MimeType,
    string? LicenseCode,
    bool ProviderRequiresAttribution,
    string? Creator,
    Uri? SourcePageUrl);

/// <summary>
/// A source of plant images (ARCHITECTURE §21). Implementations live in Infrastructure, keep their own
/// response types private, and never throw for provider failures: they return no candidates. Only
/// cancellation by the caller is thrown.
/// </summary>
public interface IPlantImageSource
{
    PlantImageKind Kind { get; }

    /// <summary>Candidates for exactly this scientific name, in the provider's own order of preference.</summary>
    Task<IReadOnlyList<ExternalImageCandidate>> GetCandidatesAsync(string scientificName, CancellationToken cancellationToken);
}
