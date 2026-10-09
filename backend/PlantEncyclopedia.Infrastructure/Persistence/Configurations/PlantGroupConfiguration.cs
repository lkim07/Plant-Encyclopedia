using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

internal sealed class PlantGroupConfiguration : IEntityTypeConfiguration<PlantGroup>
{
    public void Configure(EntityTypeBuilder<PlantGroup> b)
    {
        b.ToTable("plant_groups", t =>
            t.HasCheckConstraint("ck_plant_groups_slug", $"slug ~ {SqlConstraints.SlugPattern}"));

        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Slug).HasMaxLength(100);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Category)
            .WithMany(c => c.PlantGroups)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ux_plant_groups_slug");
        b.HasIndex(x => x.CategoryId).HasDatabaseName("ix_plant_groups_category_id");
    }
}
