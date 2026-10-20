using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Application.Explore;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Queries;

/// <summary>
/// The rule-based feed for visitors without behavioral data (API_SPEC §22, UX §13). One parameterized statement:
/// <list type="number">
/// <item>Within a plant group, plants with an image come first, then by name and ID.</item>
/// <item>Within a category, the groups take turns: every group's first plant, then every group's second, …</item>
/// <item>Across the feed, the categories take turns in the same way, in name order.</item>
/// </list>
/// Nothing is random and no popularity is used, so the order is the same on every request and pages never
/// repeat or skip a plant. Each plant row appears once; cultivars are separate plants.
/// </summary>
internal sealed class ExploreQueries(PlantEncyclopediaDbContext db) : IExploreQueries
{
    public async Task<ExplorePage> GetFeedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var published = PlantStatus.Published.ToString();
        var photo = PlantImageKind.Photo.ToString();
        var offset = (page - 1) * pageSize;
        var limit = pageSize + 1; // one extra row tells us whether another page exists

        var rows = await db.Database.SqlQuery<FeedRow>($"""
            WITH cards AS (
                SELECT p.id, p.display_name, g.name AS plant_group, g.slug AS group_slug, c.name AS category_name, c.slug AS category_slug,
                       (SELECT coalesce(i.medium_url, i.url)
                        FROM plant_images i
                        WHERE i.plant_id = p.id
                        ORDER BY CASE WHEN i.kind = {photo} THEN 0 ELSE 1 END, i.created_at
                        LIMIT 1) AS image_url
                FROM plants p
                JOIN plant_groups g ON g.id = p.plant_group_id
                JOIN categories c ON c.id = g.category_id
                WHERE p.status = {published}
            ),
            group_turns AS (
                SELECT *, row_number() OVER (
                    PARTITION BY group_slug ORDER BY (image_url IS NULL), display_name, id) AS group_turn
                FROM cards
            ),
            category_turns AS (
                SELECT *, row_number() OVER (
                    PARTITION BY category_slug ORDER BY group_turn, group_slug) AS category_turn
                FROM group_turns
            )
            SELECT id, display_name AS name, plant_group, image_url
            FROM category_turns
            ORDER BY category_turn, category_name, category_slug
            OFFSET {offset} LIMIT {limit}
            """).ToListAsync(cancellationToken);

        var hasNext = rows.Count > pageSize;
        return new ExplorePage(
            rows.Take(pageSize).Select(r => new ExploreCard(r.Id, r.Name, r.PlantGroup, r.ImageUrl)).ToList(),
            hasNext);
    }

    private sealed class FeedRow
    {
        public Guid Id { get; init; }
        public required string Name { get; init; }
        public string? PlantGroup { get; init; }
        public string? ImageUrl { get; init; }
    }
}
