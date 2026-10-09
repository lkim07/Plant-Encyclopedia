using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories", t =>
            t.HasCheckConstraint("ck_categories_slug", $"slug ~ {SqlConstraints.SlugPattern}"));

        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Slug).HasMaxLength(100);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_categories_name");
        b.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ux_categories_slug");
    }
}
