using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

internal sealed class PlantNameConfiguration : IEntityTypeConfiguration<PlantName>
{
    public void Configure(EntityTypeBuilder<PlantName> b)
    {
        b.ToTable("plant_names", t =>
        {
            t.HasCheckConstraint("ck_plant_names_name_type", SqlConstraints.InEnum<PlantNameType>("name_type"));
            t.HasCheckConstraint(
                "ck_plant_names_common_language",
                $"name_type <> '{nameof(PlantNameType.Common)}' OR language_code IS NOT NULL");
            t.HasCheckConstraint(
                "ck_plant_names_language_code_shape",
                $"language_code IS NULL OR language_code ~ {SqlConstraints.LanguageTagPattern}");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.NameType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.LanguageCode).HasMaxLength(35);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Plant)
            .WithMany(p => p.Names)
            .HasForeignKey(x => x.PlantId)
            .OnDelete(DeleteBehavior.Cascade);

        // NULLS NOT DISTINCT: two identical names with a NULL language code are duplicates.
        // plant_id leads this index, so it also serves the foreign key.
        b.HasIndex(x => new { x.PlantId, x.NameType, x.Name, x.LanguageCode })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_plant_names_plant_type_name_language");
    }
}
