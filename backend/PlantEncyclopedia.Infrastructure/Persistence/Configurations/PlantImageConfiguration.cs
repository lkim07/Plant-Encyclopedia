using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

internal sealed class PlantImageConfiguration : IEntityTypeConfiguration<PlantImage>
{
    public void Configure(EntityTypeBuilder<PlantImage> b)
    {
        b.ToTable("plant_images", t =>
        {
            t.HasCheckConstraint("ck_plant_images_width", "width IS NULL OR width > 0");
            t.HasCheckConstraint("ck_plant_images_height", "height IS NULL OR height > 0");
            t.HasCheckConstraint("ck_plant_images_kind", SqlConstraints.InEnum<PlantImageKind>("kind"));
        });

        b.HasKey(x => x.Id);

        // The database default covers rows that existed before the column. The sentinel is a value Kind never
        // has, so EF always writes Kind instead of leaving Photo (the CLR default) to the database.
        b.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(PlantImageKind.Photo)
            .HasSentinel((PlantImageKind)(-1));
        b.Property(x => x.Url).HasMaxLength(2048);
        b.Property(x => x.ThumbnailUrl).HasMaxLength(2048);
        b.Property(x => x.MediumUrl).HasMaxLength(2048);
        b.Property(x => x.LargeUrl).HasMaxLength(2048);
        b.Property(x => x.Source).HasMaxLength(100);
        // Commons file titles can be long ("File:Köhler's Medizinal-Pflanzen in naturgetreuen Abbildungen …").
        b.Property(x => x.SourceId).HasMaxLength(500);
        b.Property(x => x.SourceUrl).HasMaxLength(2048);
        b.Property(x => x.AltText).HasMaxLength(500);
        b.Property(x => x.License).HasMaxLength(200);
        b.Property(x => x.Attribution).HasMaxLength(500);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Plant)
            .WithMany(p => p.Images)
            .HasForeignKey(x => x.PlantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.PlantId).HasDatabaseName("ix_plant_images_plant_id");

        // The same external image is stored once per plant.
        b.HasIndex(x => new { x.PlantId, x.Source, x.SourceId })
            .IsUnique()
            .HasFilter("source_id IS NOT NULL")
            .HasDatabaseName("ux_plant_images_plant_source");
    }
}
