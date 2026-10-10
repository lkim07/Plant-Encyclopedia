namespace PlantEncyclopedia.Application.Plants;

public interface IPlantQueries
{
    /// <summary>
    /// Returns the plant only if its status is Published; otherwise null.
    /// Care is included only when it has been Verified, so unverified care is never shown as verified.
    /// </summary>
    Task<PlantDetail?> GetPublishedPlantAsync(Guid plantId, CancellationToken cancellationToken);
}
