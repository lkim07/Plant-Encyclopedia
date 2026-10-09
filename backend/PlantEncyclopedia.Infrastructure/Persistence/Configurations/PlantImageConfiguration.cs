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
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Url).HasMaxLength(2048);
        b.Property(x => x.ThumbnailUrl).HasMaxLength(2048);
        b.Property(x => x.MediumUrl).HasMaxLength(2048);
        b.Property(x => x.LargeUrl).HasMaxLength(2048);
        b.Property(x => x.Source).HasMaxLength(100);
        b.Property(x => x.SourceId).HasMaxLength(200);
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
    }
}
