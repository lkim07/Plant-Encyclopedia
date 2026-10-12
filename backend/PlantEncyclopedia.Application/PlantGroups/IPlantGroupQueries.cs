namespace PlantEncyclopedia.Application.PlantGroups;

public interface IPlantGroupQueries
{
    /// <summary>
    /// The plant group with this slug and one page of its plants; null if no such group exists.
    /// Only Published plants are included, ordered by name and then ID so pages are stable.
    /// <paramref name="page"/> is 1-based; callers validate page and page size.
    /// </summary>
    Task<PlantGroupDetail?> GetPlantGroupAsync(
        string slug,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
