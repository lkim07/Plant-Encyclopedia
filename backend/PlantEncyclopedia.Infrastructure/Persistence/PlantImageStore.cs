using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantEncyclopedia.Application.ExternalData.Images;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence;

internal sealed class PlantImageStore(PlantEncyclopediaDbContext db) : IPlantImageStore
{
    public const string UniqueSourceIndexName = "ux_plant_images_plant_source";

    public async Task<PlantImageTarget?> FindTargetAsync(Guid plantId, CancellationToken cancellationToken)
    {
        var plant = await db.Plants
            .AsNoTracking()
            .Where(p => p.Id == plantId)
            .Select(p => new
            {
                p.DisplayName,
                p.ScientificName,
                p.CultivarName,
                Images = p.Images.Select(i => new { i.Kind, i.Source, i.SourceId, i.RejectedAt }).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return plant is null
            ? null
            : new PlantImageTarget(
                plant.DisplayName,
                plant.ScientificName,
                plant.CultivarName,
                plant.Images.Where(i => i.RejectedAt == null).Select(i => i.Kind).Distinct().ToList(),
                plant.Images
                    .Where(i => i.SourceId != null)
                    .Select(i => (i.Source, i.SourceId!))
                    .ToHashSet());
    }

    public async Task<bool> AddAsync(PlantImage image, CancellationToken cancellationToken)
    {
        db.PlantImages.Add(image);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UniqueSourceIndexName
        })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<IReadOnlyList<Guid>> FindPlantsWithoutImagesAsync(int limit, CancellationToken cancellationToken) =>
        await db.Plants
            .AsNoTracking()
            .Where(p => p.CultivarName == null
                && p.Status != PlantStatus.Archived
                && !p.Images.Any(i => i.Kind == PlantImageKind.Photo && i.RejectedAt == null))
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(p => p.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    // The same order the API uses for the hero: photos first, then oldest first.
    public async Task<bool> RejectHeroImageAsync(Guid plantId, DateTimeOffset rejectedAt, CancellationToken cancellationToken)
    {
        var hero = await db.PlantImages
            .Where(i => i.PlantId == plantId && i.RejectedAt == null)
            .OrderBy(i => i.Kind == PlantImageKind.Photo ? 0 : 1)
            .ThenBy(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (hero is null) return false;

        hero.RejectedAt = rejectedAt;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
