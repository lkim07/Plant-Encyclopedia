using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantEncyclopedia.Domain.Retail;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

internal sealed class RetailerConfiguration : IEntityTypeConfiguration<Retailer>
{
    public void Configure(EntityTypeBuilder<Retailer> b)
    {
        b.ToTable("retailers", t =>
        {
            t.HasCheckConstraint("ck_retailers_kind", SqlConstraints.InEnum<RetailerKind>("kind"));
            t.HasCheckConstraint("ck_retailers_latitude", "latitude BETWEEN -90 AND 90");
            t.HasCheckConstraint("ck_retailers_longitude", "longitude BETWEEN -180 AND 180");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Source).HasMaxLength(32);
        b.Property(x => x.SourceId).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Street).HasMaxLength(200);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Region).HasMaxLength(100);
        b.Property(x => x.Postcode).HasMaxLength(20);
        b.Property(x => x.Website).HasMaxLength(500);

        // One row per record at its source, so a refresh updates rows instead of duplicating them.
        b.HasIndex(x => new { x.Source, x.SourceId }).IsUnique().HasDatabaseName("ux_retailers_source_id");
        // Nearby searches first narrow by a latitude band.
        b.HasIndex(x => x.Latitude).HasDatabaseName("ix_retailers_latitude");
    }
}
