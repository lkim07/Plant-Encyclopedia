using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

internal sealed class TaxonomyConfiguration : IEntityTypeConfiguration<Taxonomy>
{
    public void Configure(EntityTypeBuilder<Taxonomy> b)
    {
        b.ToTable("taxonomies");

        b.HasKey(x => x.Id);

        b.Property(x => x.Kingdom).HasMaxLength(100);
        b.Property(x => x.Phylum).HasMaxLength(100);
        b.Property(x => x.Class).HasMaxLength(100);
        b.Property(x => x.TaxonOrder).HasMaxLength(100);
        b.Property(x => x.Family).HasMaxLength(100);
        b.Property(x => x.Genus).HasMaxLength(100);
        b.Property(x => x.Species).HasMaxLength(100);
        b.Property(x => x.Subspecies).HasMaxLength(100);
        b.Property(x => x.Variety).HasMaxLength(100);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
    }
}
