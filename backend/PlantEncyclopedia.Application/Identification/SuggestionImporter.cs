using PlantEncyclopedia.Application.ExternalData;

namespace PlantEncyclopedia.Application.Identification;

/// <summary>Brings a suggested species into the encyclopedia when it is missing (D-072).</summary>
public interface ISuggestionImporter
{
    /// <summary>The plant's ID when the name is (now) an encyclopedia plant, or null when it could not be imported.</summary>
    Task<Guid?> ImportAsync(string scientificName, CancellationToken cancellationToken);
}

/// <summary>
/// Imports a suggestion through Catalogue of Life with the same pipeline as "Search Catalogue of Life"
/// (<see cref="PlantIngestionService"/>): only an exact, accepted Plantae species, subspecies or variety is saved, with
/// Catalogue of Life's taxonomy and common names, never anything from the identification provider but the name. The
/// GBIF budget and the 24-hour cache of unimportable names apply. Unlike search imports, a plant whose genus has no
/// plant group is published without one. Whether the plant is shown is decided by the caller (Published only).
/// </summary>
public sealed class CatalogueOfLifeSuggestionImporter(PlantIngestionService ingestion) : ISuggestionImporter
{
    public async Task<Guid?> ImportAsync(string scientificName, CancellationToken cancellationToken)
    {
        var result = await ingestion.ImportAsync(scientificName, cancellationToken, publishWithoutPlantGroup: true);
        return result.Status is PlantImportStatus.Created or PlantImportStatus.AlreadyExists ? result.PlantId : null;
    }
}
