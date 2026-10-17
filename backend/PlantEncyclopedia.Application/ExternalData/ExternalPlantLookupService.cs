using PlantEncyclopedia.Application.Plants;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Application.ExternalData;

public enum ExternalLookupStatus
{
    /// <summary>A Published plant: shown to the user.</summary>
    Found,

    /// <summary>The plant exists but is not published yet (for example NeedsReview after import).</summary>
    PendingReview,

    /// <summary>No importable plant: no match, a rejected match, an archived plant, or text that is not name-shaped.</summary>
    NotFound,

    /// <summary>The provider could not answer (disabled, over its limit, timed out or failed).</summary>
    Unavailable
}

public sealed record ExternalLookupResult(ExternalLookupStatus Status, PlantSummary? Plant = null);

/// <summary>
/// The "Search Catalogue of Life" action (API_SPEC §27): imports a plant the local search did not find,
/// using <see cref="PlantIngestionService"/>, and reports what the user may see. Plant data is returned
/// only for Published plants, so unreviewed imports are never revealed.
/// </summary>
public sealed class ExternalPlantLookupService(PlantIngestionService ingestion, IPlantQueries plants)
{
    public async Task<ExternalLookupResult> LookUpAsync(string searchText, CancellationToken cancellationToken)
    {
        var name = ExternalLookupName.TryPrepare(searchText);
        if (name is null)
        {
            return new ExternalLookupResult(ExternalLookupStatus.NotFound);
        }

        var import = await ingestion.ImportAsync(name, cancellationToken);
        if (import.Status == PlantImportStatus.Unavailable)
        {
            return new ExternalLookupResult(ExternalLookupStatus.Unavailable);
        }

        if (import.PlantId is not { } plantId)
        {
            return new ExternalLookupResult(ExternalLookupStatus.NotFound);
        }

        var plant = await plants.GetPlantSummaryAsync(plantId, cancellationToken);
        return plant?.Status switch
        {
            PlantStatus.Published => new ExternalLookupResult(ExternalLookupStatus.Found, plant),
            null or PlantStatus.Archived => new ExternalLookupResult(ExternalLookupStatus.NotFound),
            _ => new ExternalLookupResult(ExternalLookupStatus.PendingReview)
        };
    }
}
