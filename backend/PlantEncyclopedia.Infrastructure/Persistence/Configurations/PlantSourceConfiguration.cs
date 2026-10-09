using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

internal sealed class PlantSourceConfiguration : IEntityTypeConfiguration<PlantSource>
{
    public void Configure(EntityTypeBuilder<PlantSource> b)
    {
        b.ToTable("plant_sources", t =>
        {
            t.HasCheckConstraint("ck_plant_sources_source_type", SqlConstraints.InEnum<PlantSourceType>("source_type"));
            t.HasCheckConstraint("ck_plant_sources_identifier", "external_id IS NOT NULL OR source_url IS NOT NULL");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Provider).HasMaxLength(100);
        b.Property(x => x.ExternalId).HasMaxLength(200);
        b.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.SourceUrl).HasMaxLength(2048);

        // Restrict: provenance must not be deleted silently with its plant.
        b.HasOne(x => x.Plant)
            .WithMany(p => p.Sources)
            .HasForeignKey(x => x.PlantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevents duplicate provenance for the same plant and source type.
        // Does not prevent duplicate Plant rows from concurrent ingestion.
        b.HasIndex(x => new { x.PlantId, x.Provider, x.ExternalId, x.SourceType })
            .IsUnique()
            .HasFilter("external_id IS NOT NULL")
            .HasDatabaseName("ux_plant_sources_external_id");

        // Sources without an external ID (e.g. curated references) are identified by URL.
        b.HasIndex(x => new { x.PlantId, x.Provider, x.SourceUrl, x.SourceType })
            .IsUnique()
            .HasFilter("external_id IS NULL")
            .HasDatabaseName("ux_plant_sources_source_url");

        // The partial indexes above cannot serve plant_id lookups that lack an external_id predicate.
        b.HasIndex(x => x.PlantId).HasDatabaseName("ix_plant_sources_plant_id");
    }
}
