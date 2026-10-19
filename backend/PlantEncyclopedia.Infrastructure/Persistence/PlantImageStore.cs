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
            .Select(p => new { p.DisplayName, p.ScientificName, p.CultivarName, Kinds = p.Images.Select(i => i.Kind).ToList() })
            .SingleOrDefaultAsync(cancellationToken);

        return plant is null
            ? null
            : new PlantImageTarget(plant.DisplayName, plant.ScientificName, plant.CultivarName, plant.Kinds.Distinct().ToList());
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
            .Where(p => p.CultivarName == null && p.Status != PlantStatus.Archived && !p.Images.Any(i => i.Kind == PlantImageKind.Photo))
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(p => p.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
