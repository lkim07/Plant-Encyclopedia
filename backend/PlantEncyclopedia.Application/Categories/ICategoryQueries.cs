namespace PlantEncyclopedia.Application.Categories;

public interface ICategoryQueries
{
    /// <summary>All categories, ordered by name.</summary>
    Task<IReadOnlyList<CategorySummary>> GetCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The category with this slug and only its own plant groups, ordered by name; null if none exists.
    /// </summary>
    Task<CategoryDetail?> GetCategoryAsync(string slug, CancellationToken cancellationToken);
}
