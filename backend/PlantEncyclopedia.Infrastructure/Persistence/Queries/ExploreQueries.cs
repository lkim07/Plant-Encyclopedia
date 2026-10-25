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
/// Signed-in users with enough activity get the personalized order instead (<see cref="GetFeedForUserAsync"/>).
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
                        WHERE i.plant_id = p.id AND i.rejected_at IS NULL
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

    /// <summary>
    /// Loads the user's signals and every feed plant, orders them in memory (<see cref="PersonalizedFeed"/>), then
    /// loads the cards of the requested page. Fine for the current catalogue; a larger one would need the order cached.
    /// </summary>
    public async Task<ExplorePage> GetFeedForUserAsync(
        Guid userId,
        DateTimeOffset asOf,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var signals = await LoadSignalsAsync(userId, asOf, cancellationToken);
        if (signals.Sum(s => s.Weight) < PersonalizedFeed.MinInterest)
        {
            return await GetFeedAsync(page, pageSize, cancellationToken);
        }

        var plants = await db.Plants
            .AsNoTracking()
            .Where(p => p.Status == PlantStatus.Published && p.PlantGroupId != null)
            .Select(p => new FeedPlant(
                p.Id,
                p.PlantGroupId!.Value,
                p.PlantGroup!.CategoryId,
                p.DisplayName,
                p.Images.Any(i => i.RejectedAt == null),
                db.ViewHistory.Any(v => v.UserId == userId && v.PlantId == p.Id && v.ViewedAt <= asOf)
                    || db.Favorites.Any(f => f.UserId == userId && f.PlantId == p.Id && f.CreatedAt <= asOf)))
            .ToListAsync(cancellationToken);

        var order = PersonalizedFeed.Order(plants, signals, asOf);
        if (order is null)
        {
            return await GetFeedAsync(page, pageSize, cancellationToken);
        }

        var offset = (page - 1) * pageSize;
        var ids = order.Skip(offset).Take(pageSize).ToList();
        var cards = await db.Plants
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new ExploreCard(
                p.Id,
                p.DisplayName,
                p.PlantGroup != null ? p.PlantGroup.Name : null,
                p.Images
                    .Where(i => i.RejectedAt == null)
                    .OrderBy(i => i.Kind == PlantImageKind.Photo ? 0 : 1)
                    .ThenBy(i => i.CreatedAt)
                    .Select(i => i.MediumUrl ?? i.Url)
                    .FirstOrDefault()))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        return new ExplorePage(ids.Where(cards.ContainsKey).Select(id => cards[id]).ToList(), order.Count > offset + pageSize);
    }

    /// <summary>
    /// Views and saved plants of Published plants, and searches: each search's weight is shared among the plant groups
    /// whose plants or group name contain the whole search text (folded with search_key, like Search). A removed
    /// search or favorite stops counting at once.
    /// </summary>
    private async Task<List<FeedSignal>> LoadSignalsAsync(Guid userId, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var signals = await db.ViewHistory
            .AsNoTracking()
            .Where(v => v.UserId == userId && v.ViewedAt <= asOf && v.Plant!.Status == PlantStatus.Published && v.Plant.PlantGroupId != null)
            .Select(v => new FeedSignal(v.Plant!.PlantGroupId!.Value, PersonalizedFeed.ViewWeight, v.ViewedAt))
            .ToListAsync(cancellationToken);

        signals.AddRange(await db.Favorites
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.CreatedAt <= asOf && f.Plant!.Status == PlantStatus.Published && f.Plant.PlantGroupId != null)
            .Select(f => new FeedSignal(f.Plant!.PlantGroupId!.Value, PersonalizedFeed.FavoriteWeight, f.CreatedAt))
            .ToListAsync(cancellationToken));

        // One parameterized statement; the search text never becomes SQL.
        var published = PlantStatus.Published.ToString();
        var matches = await db.Database.SqlQuery<SearchMatchRow>($"""
            SELECT s.id AS search_id, s.searched_at, p.plant_group_id AS group_id
            FROM search_history s
            CROSS JOIN LATERAL (SELECT public.search_key(s.query) AS k) q
            JOIN plants p ON p.status = {published} AND p.plant_group_id IS NOT NULL
            JOIN plant_groups g ON g.id = p.plant_group_id
            WHERE s.user_id = {userId} AND s.searched_at <= {asOf} AND q.k <> ''
              AND (strpos(public.search_key(p.display_name), q.k) > 0
                OR strpos(public.search_key(p.scientific_name), q.k) > 0
                OR strpos(coalesce(public.search_key(p.cultivar_name), ''), q.k) > 0
                OR strpos(public.search_key(g.name), q.k) > 0
                OR EXISTS (SELECT 1 FROM plant_names n WHERE n.plant_id = p.id AND strpos(public.search_key(n.name), q.k) > 0))
            GROUP BY s.id, s.searched_at, p.plant_group_id
            """).ToListAsync(cancellationToken);

        foreach (var search in matches.GroupBy(m => m.SearchId))
        {
            var weight = PersonalizedFeed.SearchWeight / search.Count();
            signals.AddRange(search.Select(m => new FeedSignal(m.GroupId, weight, m.SearchedAt)));
        }

        return signals;
    }

    private sealed class FeedRow
    {
        public Guid Id { get; init; }
        public required string Name { get; init; }
        public string? PlantGroup { get; init; }
        public string? ImageUrl { get; init; }
    }

    private sealed class SearchMatchRow
    {
        public Guid SearchId { get; init; }
        public DateTimeOffset SearchedAt { get; init; }
        public Guid GroupId { get; init; }
    }
}
