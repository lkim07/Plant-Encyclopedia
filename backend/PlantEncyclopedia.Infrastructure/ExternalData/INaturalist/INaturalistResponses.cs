namespace PlantEncyclopedia.Infrastructure.ExternalData.INaturalist;

// The parts of GET /v1/taxa and GET /v1/taxa/{id} that the photo source reads (snake_case JSON).
// Recorded examples: PlantEncyclopedia.Tests/Fixtures/INaturalist. These types never leave the adapter.

internal sealed record INaturalistTaxaResponse(List<INaturalistTaxon?>? Results);

/// <param name="Rank">"species", "hybrid", "subspecies", "variety", "section", ...</param>
/// <param name="IconicTaxonName">"Plantae", "Mollusca", ...</param>
/// <param name="TaxonPhotos">Only in the taxon details response, in curator order.</param>
internal sealed record INaturalistTaxon(
    long Id,
    string? Name,
    string? Rank,
    bool? IsActive,
    string? IconicTaxonName,
    List<INaturalistTaxonPhoto?>? TaxonPhotos);

/// <param name="TaxonId">The taxon the photo shows; it can be a descendant of the requested taxon.</param>
internal sealed record INaturalistTaxonPhoto(long? TaxonId, INaturalistPhoto? Photo);

/// <param name="LicenseCode">"cc0", "cc-by", "cc-by-sa", "cc-by-nc", ... or null for all rights reserved.</param>
/// <param name="AttributionName">The photographer's name.</param>
internal sealed record INaturalistPhoto(
    long Id,
    string? LicenseCode,
    string? AttributionName,
    string? OriginalUrl,
    INaturalistDimensions? OriginalDimensions);

internal sealed record INaturalistDimensions(int Width, int Height);
