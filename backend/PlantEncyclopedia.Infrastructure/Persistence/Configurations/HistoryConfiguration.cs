using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantEncyclopedia.Domain.History;
using PlantEncyclopedia.Infrastructure.Identity;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

internal sealed class SearchHistoryConfiguration : IEntityTypeConfiguration<SearchHistoryEntry>
{
    public void Configure(EntityTypeBuilder<SearchHistoryEntry> b)
    {
        b.ToTable("search_history");

        b.HasKey(x => x.Id);
        b.Property(x => x.Query).HasMaxLength(100);
        b.Property(x => x.QueryKey).HasMaxLength(100);

        // History goes when its user goes.
        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One entry per user and text (ignoring case); user_id leads, so it also serves the user foreign key.
        b.HasIndex(x => new { x.UserId, x.QueryKey }).IsUnique().HasDatabaseName("ux_search_history_user_query_key");
        b.HasIndex(x => new { x.UserId, x.SearchedAt }).HasDatabaseName("ix_search_history_user_searched_at");
    }
}

internal sealed class ViewHistoryConfiguration : IEntityTypeConfiguration<ViewHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ViewHistoryEntry> b)
    {
        b.ToTable("view_history");

        b.HasKey(x => x.Id);

        // A view goes when its user or its plant goes.
        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Plant)
            .WithMany()
            .HasForeignKey(x => x.PlantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.UserId, x.ViewedAt }).HasDatabaseName("ix_view_history_user_viewed_at");
        b.HasIndex(x => x.PlantId).HasDatabaseName("ix_view_history_plant_id");
    }
}
