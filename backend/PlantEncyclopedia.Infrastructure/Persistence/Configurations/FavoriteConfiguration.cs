using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantEncyclopedia.Domain.Favorites;
using PlantEncyclopedia.Infrastructure.Identity;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

internal sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> b)
    {
        b.ToTable("favorites", t => t.HasCheckConstraint("ck_favorites_position", "position > 0"));

        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        // A favorite goes when its user or its plant goes.
        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Plant)
            .WithMany()
            .HasForeignKey(x => x.PlantId)
            .OnDelete(DeleteBehavior.Cascade);

        // One favorite per user and plant (DATABASE_DESIGN §26); user_id leads, so it also serves the user foreign key.
        b.HasIndex(x => new { x.UserId, x.PlantId }).IsUnique().HasDatabaseName("ux_favorites_user_plant");
        b.HasIndex(x => new { x.UserId, x.Position }).HasDatabaseName("ix_favorites_user_position");
        b.HasIndex(x => x.PlantId).HasDatabaseName("ix_favorites_plant_id");
    }
}
