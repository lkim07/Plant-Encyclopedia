using PlantEncyclopedia.Application.ExternalData.Images;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Application.ExternalData;

public enum PlantImportStatus
{
    /// <summary>A new plant was saved (Published or NeedsReview).</summary>
    Created,

    /// <summary>The plant already exists; its ID is returned and nothing was changed.</summary>
    AlreadyExists,

    /// <summary>The requested name is blank, too long or contains control characters.</summary>
    InvalidName,

    /// <summary>The provider has no match.</summary>
    NotFound,

    /// <summary>The provider's match is not importable (see <see cref="TaxonRejection"/>).</summary>
    Rejected,

    /// <summary>The provider could not be reached or answered with an error. Nothing was saved.</summary>
    Unavailable
}

public sealed record PlantImportResult(
    PlantImportStatus Status,
    Guid? PlantId = null,
    PlantStatus? PlantStatus = null,
    TaxonRejection? Rejection = null);

/// <summary>
/// Imports one plant from an external taxonomy provider (ARCHITECTURE §22–23). Existing plants are
/// reused and never overwritten. A new plant is Published only when its genus maps to an existing
/// plant group (or the caller allows publishing without one) and no near-duplicate name exists; otherwise it is
/// saved as NeedsReview.
/// Common names in the app's languages are imported with it; the display name is the English name chosen
/// by <see cref="CommonNamePolicy"/>, or the scientific name when there is none. A created plant is queued
/// for image import, which runs in the background (<see cref="IPlantImageQueue"/>).
/// </summary>
public sealed class PlantIngestionService(
    IPlantDataProvider provider,
    IPlantImportStore store,
    GenusPlantGroupMap plantGroups,
    IPlantImageQueue imageQueue,
    TimeProvider timeProvider)
{
    /// <param name="publishWithoutPlantGroup">
    /// Publish a plant whose genus is not mapped to a plant group, without a group (identification imports, D-072).
    /// Such a plant has a detail page and cards but stays out of Explore and categories until it gets a group.
    /// </param>
    public async Task<PlantImportResult> ImportAsync(
        string scientificName,
        CancellationToken cancellationToken,
        bool publishWithoutPlantGroup = false)
    {
        var requested = ExternalTaxonPolicy.NormalizeRequestedName(scientificName);
        if (requested is null)
        {
            return new PlantImportResult(PlantImportStatus.InvalidName);
        }

        // A known plant needs no external request.
        if (await store.FindByIdentityAsync(requested, cancellationToken) is { } knownId)
        {
            return new PlantImportResult(PlantImportStatus.AlreadyExists, knownId);
        }

        var lookup = await provider.MatchAsync(requested, cancellationToken);
        switch (lookup.Status)
        {
            case TaxonLookupStatus.Unavailable:
                return new PlantImportResult(PlantImportStatus.Unavailable);
            case TaxonLookupStatus.NotFound:
                return new PlantImportResult(PlantImportStatus.NotFound);
        }

        if (lookup.Taxon is null)
        {
            return new PlantImportResult(PlantImportStatus.NotFound);
        }

        if (ExternalTaxonPolicy.Evaluate(lookup.Taxon, out var taxon) is { } rejection)
        {
            return new PlantImportResult(PlantImportStatus.Rejected, Rejection: rejection);
        }

        // The provider's spelling may differ from the request ("Mentha piperita" → "Mentha × piperita").
        var existingId =
            await store.FindByTaxonSourceAsync(taxon.Provider, taxon.ExternalId, cancellationToken)
            ?? await store.FindByIdentityAsync(taxon.ScientificName, cancellationToken);
        if (existingId is { } id)
        {
            return new PlantImportResult(PlantImportStatus.AlreadyExists, id);
        }

        var slug = plantGroups.FindSlug(taxon.Classification.Genus);
        var plantGroupId = slug is null ? null : await store.FindPlantGroupIdAsync(slug, cancellationToken);
        var nearDuplicate = await store.HasNearDuplicateAsync(taxon.ScientificName, cancellationToken);
        var status = (plantGroupId is not null || publishWithoutPlantGroup) && !nearDuplicate
            ? PlantStatus.Published
            : PlantStatus.NeedsReview;

        // Only for plants about to be created: existing plants are never changed.
        var commonNames = CommonNamePolicy.Select(await provider.GetCommonNamesAsync(taxon, cancellationToken));

        var plant = new Plant
        {
            DisplayName = commonNames.EnglishDisplayName ?? taxon.ScientificName,
            ScientificName = taxon.ScientificName,
            Names = commonNames.Names
                .Select(n => new PlantName { Name = n.Name, NameType = PlantNameType.Common, LanguageCode = n.LanguageCode })
                .ToList(),
            PlantGroupId = plantGroupId,
            Status = status,
            Taxonomy = ToTaxonomy(taxon),
            Sources =
            [
                new PlantSource
                {
                    Provider = taxon.Provider,
                    ExternalId = taxon.ExternalId,
                    SourceType = PlantSourceType.Taxonomy,
                    RetrievedAt = timeProvider.GetUtcNow()
                }
            ]
        };

        var inserted = await store.InsertAsync(plant, cancellationToken);
        if (!inserted.Created)
        {
            return new PlantImportResult(PlantImportStatus.AlreadyExists, inserted.PlantId);
        }

        imageQueue.Enqueue(inserted.PlantId);
        return new PlantImportResult(PlantImportStatus.Created, inserted.PlantId, status);
    }

    // Species holds the full binomial (as in the seed data); subspecies and variety hold the full name.
    private static Taxonomy ToTaxonomy(ExternalTaxon taxon)
    {
        var c = taxon.Classification;
        return new Taxonomy
        {
            Kingdom = c.Kingdom,
            Phylum = c.Phylum,
            Class = c.Class,
            TaxonOrder = c.Order,
            Family = c.Family,
            Genus = c.Genus,
            Species = taxon.Rank == TaxonRank.Species ? taxon.ScientificName : c.Species,
            Subspecies = taxon.Rank == TaxonRank.Subspecies ? taxon.ScientificName : null,
            Variety = taxon.Rank == TaxonRank.Variety ? taxon.ScientificName : null
        };
    }
}
