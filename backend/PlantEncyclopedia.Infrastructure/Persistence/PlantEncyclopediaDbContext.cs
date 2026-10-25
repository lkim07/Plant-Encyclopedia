using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Domain.Common;
using PlantEncyclopedia.Domain.Favorites;
using PlantEncyclopedia.Domain.History;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Infrastructure.Identity;
using PlantEncyclopedia.Infrastructure.Persistence.Configurations;

namespace PlantEncyclopedia.Infrastructure.Persistence;

/// <summary>The application database, including the user accounts kept by ASP.NET Core Identity (D-070).</summary>
public class PlantEncyclopediaDbContext(DbContextOptions<PlantEncyclopediaDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PlantGroup> PlantGroups => Set<PlantGroup>();
    public DbSet<Taxonomy> Taxonomies => Set<Taxonomy>();
    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<PlantName> PlantNames => Set<PlantName>();
    public DbSet<PlantImage> PlantImages => Set<PlantImage>();
    public DbSet<PlantSource> PlantSources => Set<PlantSource>();
    public DbSet<PlantCare> PlantCare => Set<PlantCare>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<SearchHistoryEntry> SearchHistory => Set<SearchHistoryEntry>();
    public DbSet<ViewHistoryEntry> ViewHistory => Set<ViewHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        IdentityConfiguration.Configure(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlantEncyclopediaDbContext).Assembly);

        // Search support (AddSearchSupport migration): accent removal and trigram indexes.
        // No schema argument: the snapshot generator drops it, which would leave a permanent pending change.
        // The extensions install into the default schema (public), which search_key() references explicitly.
        modelBuilder.HasPostgresExtension("unaccent");
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasDbFunction(typeof(SearchFunctions).GetMethod(nameof(SearchFunctions.SearchKey))!)
            .HasSchema("public")
            .HasName("search_key");
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
