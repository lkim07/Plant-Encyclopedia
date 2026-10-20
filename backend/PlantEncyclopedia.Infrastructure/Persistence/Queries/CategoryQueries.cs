using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Application.Categories;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Queries;

internal sealed class CategoryQueries(PlantEncyclopediaDbContext db, TimeProvider timeProvider) : ICategoryQueries
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
            .Select(g => new { g.Id, g.Slug, g.Name })
            .ToListAsync(cancellationToken);

        var images = await GroupImagesAsync(groups.Select(g => g.Id).ToArray(), cancellationToken);
        return new CategoryDetail(
            category.Slug,
            category.Name,
            groups.Select(g => new PlantGroupSummary(g.Slug, g.Name, images.GetValueOrDefault(g.Id))).ToList());
    }

    /// <summary>
    /// One card image per group: a usable image of one of its Published plants, photos before illustrations. The plant
    /// is picked by hashing its ID with today's date (UTC), so the choice looks random, stays the same all day,
    /// and changes the next day. One parameterized statement.
    /// </summary>
    private async Task<Dictionary<Guid, string>> GroupImagesAsync(Guid[] groupIds, CancellationToken cancellationToken)
    {
        if (groupIds.Length == 0) return [];

        var day = timeProvider.GetUtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var published = PlantStatus.Published.ToString();
        var photo = PlantImageKind.Photo.ToString();

        var rows = await db.Database.SqlQuery<GroupImageRow>($"""
            SELECT DISTINCT ON (p.plant_group_id) p.plant_group_id, coalesce(i.medium_url, i.url) AS image_url
            FROM plants p
            JOIN plant_images i ON i.plant_id = p.id
            WHERE p.plant_group_id = ANY({groupIds}) AND p.status = {published} AND i.rejected_at IS NULL
            ORDER BY p.plant_group_id,
                     CASE WHEN i.kind = {photo} THEN 0 ELSE 1 END,
                     md5(p.id::text || {day}),
                     i.created_at
            """).ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.PlantGroupId, r => r.ImageUrl);
    }

    private sealed class GroupImageRow
    {
        public Guid PlantGroupId { get; init; }
        public required string ImageUrl { get; init; }
    }
}
