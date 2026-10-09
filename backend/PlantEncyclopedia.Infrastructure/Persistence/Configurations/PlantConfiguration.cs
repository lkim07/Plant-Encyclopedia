using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

internal sealed class PlantConfiguration : IEntityTypeConfiguration<Plant>
{
    public void Configure(EntityTypeBuilder<Plant> b)
    {
        b.ToTable("plants", t =>
        {
            t.HasCheckConstraint("ck_plants_status", SqlConstraints.InEnum<PlantStatus>("status"));
            t.HasCheckConstraint(
                "ck_plants_published_requires_group",
                $"status <> '{nameof(PlantStatus.Published)}' OR plant_group_id IS NOT NULL");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.DisplayName).HasMaxLength(200);
        b.Property(x => x.ScientificName).HasMaxLength(300);
        b.Property(x => x.CultivarName).HasMaxLength(200);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.PlantGroup)
            .WithMany(g => g.Plants)
            .HasForeignKey(x => x.PlantGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Taxonomy)
            .WithMany(t => t.Plants)
            .HasForeignKey(x => x.TaxonomyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.PlantGroupId).HasDatabaseName("ix_plants_plant_group_id");
        b.HasIndex(x => x.TaxonomyId).HasDatabaseName("ix_plants_taxonomy_id");
        b.HasIndex(x => x.DisplayName).HasDatabaseName("ix_plants_display_name");
        b.HasIndex(x => x.ScientificName).HasDatabaseName("ix_plants_scientific_name");
    }
}
