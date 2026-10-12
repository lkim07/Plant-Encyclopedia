using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Application.PlantGroups;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Queries;

internal sealed class PlantGroupQueries(PlantEncyclopediaDbContext db) : IPlantGroupQueries
{
    public async Task<PlantGroupDetail?> GetPlantGroupAsync(
        string slug,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var group = await db.PlantGroups
            .AsNoTracking()
            .Where(g => g.Slug == slug)
            .Select(g => new { g.Id, g.Slug, g.Name, CategorySlug = g.Category!.Slug, CategoryName = g.Category.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (group is null)
        {
            return null;
        }

        // One extra row tells us whether another page exists, without a separate count query.
        var plants = await db.Plants
            .AsNoTracking()
            .Where(p => p.PlantGroupId == group.Id && p.Status == PlantStatus.Published)
            .OrderBy(p => p.DisplayName)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .Select(p => new PlantSummary(p.Id, p.DisplayName))
            .ToListAsync(cancellationToken);

        var hasNext = plants.Count > pageSize;

        return new PlantGroupDetail(
            group.Slug,
            group.Name,
            new CategoryReference(group.CategorySlug, group.CategoryName),
            hasNext ? plants.Take(pageSize).ToList() : plants,
            hasNext);
    }
}
