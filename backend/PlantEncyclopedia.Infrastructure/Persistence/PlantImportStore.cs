using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence;

internal sealed class PlantImportStore(PlantEncyclopediaDbContext db) : IPlantImportStore
{
    // Same expressions as ux_plants_identity, so the lookup uses that index. '' is the identity of "no cultivar".
    public async Task<Guid?> FindByIdentityAsync(string scientificName, CancellationToken cancellationToken)
    {
        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM plants
            WHERE public.plant_scientific_identity(scientific_name) = public.plant_scientific_identity({scientificName})
              AND public.plant_cultivar_identity(cultivar_name) = ''
            """).ToListAsync(cancellationToken);

        return ids.Count == 0 ? null : ids[0];
    }

    public Task<Guid?> FindByTaxonSourceAsync(string provider, string externalId, CancellationToken cancellationToken) =>
        db.PlantSources
            .Where(s => s.Provider == provider
                && s.ExternalId == externalId
                && s.SourceType == PlantSourceType.Taxonomy
                && s.Plant!.CultivarName == null)
            .Select(s => (Guid?)s.PlantId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Guid?> FindPlantGroupIdAsync(string slug, CancellationToken cancellationToken) =>
        db.PlantGroups
            .Where(g => g.Slug == slug)
            .Select(g => (Guid?)g.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasNearDuplicateAsync(string scientificName, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM plants
                WHERE public.search_key(scientific_name) = public.search_key({scientificName})
                  AND public.plant_scientific_identity(scientific_name) <> public.plant_scientific_identity({scientificName})
            ) AS "Value"
            """).SingleAsync(cancellationToken);

    // SaveChanges inserts the taxonomy, plant and sources in one transaction. A concurrent insert of the
    // same identity makes this one wait and then fail with unique_violation; the winner is returned.
    public async Task<PlantInsertResult> InsertAsync(Plant plant, CancellationToken cancellationToken)
    {
        db.Plants.Add(plant);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new PlantInsertResult(plant.Id, Created: true);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: PlantIdentity.UniqueIndexName
        })
        {
            db.ChangeTracker.Clear();

            var existingId = plant.CultivarName is null
                ? await FindByIdentityAsync(plant.ScientificName, cancellationToken)
                : null;

            return existingId is { } id
                ? new PlantInsertResult(id, Created: false)
                : throw new InvalidOperationException("The conflicting plant could not be found.", e);
        }
    }
}
