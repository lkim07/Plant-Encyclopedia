using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Application.ExternalData;

/// <summary>Database operations needed to import an external taxon (implemented in Infrastructure).</summary>
public interface IPlantImportStore
{
    /// <summary>The species-level plant (no cultivar) with the same identity (ux_plants_identity), whatever its status.</summary>
    Task<Guid?> FindByIdentityAsync(string scientificName, CancellationToken cancellationToken);

    /// <summary>The species-level plant (no cultivar) already linked to this provider taxon, if any.</summary>
    Task<Guid?> FindByTaxonSourceAsync(string provider, string externalId, CancellationToken cancellationToken);

    Task<Guid?> FindPlantGroupIdAsync(string slug, CancellationToken cancellationToken);

    /// <summary>
    /// True when another plant's scientific name reads the same once accents, case and punctuation are
    /// ignored (search_key) but has a different identity, e.g. "Mentha piperita" vs "Mentha × piperita".
    /// </summary>
    Task<bool> HasNearDuplicateAsync(string scientificName, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the plant with its taxonomy and sources in one transaction. If another request inserted
    /// the same identity first, nothing is saved and the existing plant is returned instead.
    /// </summary>
    Task<PlantInsertResult> InsertAsync(Plant plant, CancellationToken cancellationToken);
}

public sealed record PlantInsertResult(Guid PlantId, bool Created);
