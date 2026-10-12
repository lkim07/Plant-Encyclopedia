using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Application.Categories;

namespace PlantEncyclopedia.Infrastructure.Persistence.Queries;

internal sealed class CategoryQueries(PlantEncyclopediaDbContext db) : ICategoryQueries
{
    public async Task<IReadOnlyList<CategorySummary>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategorySummary(c.Slug, c.Name))
            .ToListAsync(cancellationToken);

    public async Task<CategoryDetail?> GetCategoryAsync(string slug, CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .AsNoTracking()
            .Where(c => c.Slug == slug)
            .Select(c => new { c.Id, c.Slug, c.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return null;
        }

        // Category-pure: only this category's groups (D-017).
        var groups = await db.PlantGroups
            .AsNoTracking()
            .Where(g => g.CategoryId == category.Id)
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Slug)
            .Select(g => new PlantGroupSummary(g.Slug, g.Name))
            .ToListAsync(cancellationToken);

        return new CategoryDetail(category.Slug, category.Name, groups);
    }
}
