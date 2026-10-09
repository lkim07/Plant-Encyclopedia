using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Domain.Common;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence;

public class PlantEncyclopediaDbContext(DbContextOptions<PlantEncyclopediaDbContext> options)
    : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PlantGroup> PlantGroups => Set<PlantGroup>();
    public DbSet<Taxonomy> Taxonomies => Set<Taxonomy>();
    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<PlantName> PlantNames => Set<PlantName>();
    public DbSet<PlantImage> PlantImages => Set<PlantImage>();
    public DbSet<PlantSource> PlantSources => Set<PlantSource>();
    public DbSet<PlantCare> PlantCare => Set<PlantCare>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlantEncyclopediaDbContext).Assembly);
    }

    // Timestamps are maintained here, so they apply only to writes made through SaveChanges.
    // Raw SQL, ExecuteUpdate and ExecuteDelete bypass this logic.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IHasCreatedAt>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.CreatedAt).IsModified = false;
            }
        }

        foreach (var entry in ChangeTracker.Entries<IHasTimestamps>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
