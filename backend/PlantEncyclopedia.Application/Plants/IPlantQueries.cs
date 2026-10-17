namespace PlantEncyclopedia.Application.Plants;

public interface IPlantQueries
{
    /// <summary>
    /// Returns the plant only if its status is Published; otherwise null.
    /// Care is included only when it has been Verified, so unverified care is never shown as verified.
    /// </summary>
    Task<PlantDetail?> GetPublishedPlantAsync(Guid plantId, CancellationToken cancellationToken);

    /// <summary>The plant in any status, or null. Callers must not show non-Published plants to users.</summary>
    Task<PlantSummary?> GetPlantSummaryAsync(Guid plantId, CancellationToken cancellationToken);
}
