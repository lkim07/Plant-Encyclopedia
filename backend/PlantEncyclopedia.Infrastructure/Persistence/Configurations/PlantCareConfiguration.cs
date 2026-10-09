using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

internal sealed class PlantCareConfiguration : IEntityTypeConfiguration<PlantCare>
{
    public void Configure(EntityTypeBuilder<PlantCare> b)
    {
        b.ToTable("plant_care", t =>
        {
            t.HasCheckConstraint(
                "ck_plant_care_verification_status",
                SqlConstraints.InEnum<CareVerificationStatus>("verification_status"));
            t.HasCheckConstraint(
                "ck_plant_care_verified_at",
                $"verification_status <> '{nameof(CareVerificationStatus.Verified)}' OR verified_at IS NOT NULL");
            t.HasCheckConstraint(
                "ck_plant_care_temp_min_range",
                "temperature_min_c IS NULL OR temperature_min_c BETWEEN -60 AND 60");
            t.HasCheckConstraint(
                "ck_plant_care_temp_max_range",
                "temperature_max_c IS NULL OR temperature_max_c BETWEEN -60 AND 60");
            t.HasCheckConstraint(
                "ck_plant_care_temp_order",
                "temperature_min_c IS NULL OR temperature_max_c IS NULL OR temperature_min_c <= temperature_max_c");
        });

        // One-to-one with Plant; the primary key is also the foreign key.
        b.HasKey(x => x.PlantId);

        b.Property(x => x.LightSummary).HasMaxLength(100);
        b.Property(x => x.WaterSummary).HasMaxLength(100);
        b.Property(x => x.SoilSummary).HasMaxLength(100);
        b.Property(x => x.TemperatureMinC).HasPrecision(4, 1);
        b.Property(x => x.TemperatureMaxC).HasPrecision(4, 1);
        b.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Plant)
            .WithOne(p => p.Care)
            .HasForeignKey<PlantCare>(x => x.PlantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
