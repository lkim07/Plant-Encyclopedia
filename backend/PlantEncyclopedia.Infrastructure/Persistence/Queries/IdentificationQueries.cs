using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Application.Explore;
using PlantEncyclopedia.Application.Identification;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Queries;

/// <summary>
/// Matches suggested names against Published plants with the same identity expression as ux_plants_identity
/// (plant_scientific_identity), so "Rosa  canina" and "Rosa canina" match and a hybrid sign or rank marker is kept.
/// One parameterized statement; provider text is only ever a bound parameter.
/// </summary>
internal sealed class IdentificationQueries(PlantEncyclopediaDbContext db) : IIdentificationQueries
{
    public async Task<IReadOnlyList<MatchedPlant>> FindPublishedAsync(IReadOnlyList<string> scientificNames, CancellationToken cancellationToken)
    {
        if (scientificNames.Count == 0) return [];

        var names = scientificNames.ToArray();
        var published = PlantStatus.Published.ToString();
        var photo = PlantImageKind.Photo.ToString();

        var rows = await db.Database.SqlQuery<MatchRow>($"""
            SELECT c.ord::int - 1 AS candidate_index, p.id, p.display_name AS name, g.name AS plant_group, g.slug AS plant_group_slug, p.scientific_name,
                   (SELECT coalesce(i.medium_url, i.url)
                    FROM plant_images i
                    WHERE i.plant_id = p.id AND i.rejected_at IS NULL
                    ORDER BY CASE WHEN i.kind = {photo} THEN 0 ELSE 1 END, i.created_at
                    LIMIT 1) AS image_url
            FROM unnest({names}) WITH ORDINALITY AS c(name, ord)
            JOIN plants p ON public.plant_scientific_identity(p.scientific_name) = public.plant_scientific_identity(c.name)
            LEFT JOIN plant_groups g ON g.id = p.plant_group_id
            WHERE p.status = {published}
            ORDER BY c.ord, (p.cultivar_name IS NOT NULL), p.display_name, p.id
            """).ToListAsync(cancellationToken);

        return rows.Select(r => new MatchedPlant(r.CandidateIndex, r.Id, r.Name, r.PlantGroup, r.PlantGroupSlug, r.ScientificName, r.ImageUrl)).ToList();
    }

    public Task<MatchedPlant?> FindPublishedByIdAsync(Guid plantId, CancellationToken cancellationToken) =>
        db.Plants
            .AsNoTracking()
            .Where(p => p.Id == plantId && p.Status == PlantStatus.Published)
            .Select(p => new MatchedPlant(
                0,
                p.Id,
                p.DisplayName,
                p.PlantGroup != null ? p.PlantGroup.Name : null,
                p.PlantGroup != null ? p.PlantGroup.Slug : null,
                p.ScientificName,
                p.Images
                    .Where(i => i.RejectedAt == null)
                    .OrderBy(i => i.Kind == PlantImageKind.Photo ? 0 : 1)
                    .ThenBy(i => i.CreatedAt)
                    .Select(i => i.MediumUrl ?? i.Url)
                    .FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ExplorePage> GetRelatedAsync(IReadOnlyList<Guid> candidateIds, int page, int pageSize, CancellationToken cancellationToken)
    {
        var ids = candidateIds.ToArray();
        var published = PlantStatus.Published.ToString();
        var photo = PlantImageKind.Photo.ToString();
        var offset = (page - 1) * pageSize;
        var limit = pageSize + 1; // one extra row tells us whether another page exists

        // closeness = tier * 10 + the candidate's position (1–3): tier 1 same group, 2 genus, 3 family, 4 category.
        var rows = await db.Database.SqlQuery<RelatedRow>($"""
            WITH seeds AS (
                SELECT s.ord::int AS ord, p.plant_group_id, g.category_id, t.genus, t.family
                FROM unnest({ids}) WITH ORDINALITY AS s(id, ord)
                JOIN plants p ON p.id = s.id AND p.status = {published}
                LEFT JOIN plant_groups g ON g.id = p.plant_group_id
                LEFT JOIN taxonomies t ON t.id = p.taxonomy_id
            ),
            related AS (
                SELECT p.id, p.display_name, g.name AS plant_group,
                       (SELECT min(CASE
                                       WHEN s.plant_group_id = p.plant_group_id THEN 1
                                       WHEN s.genus = t.genus THEN 2
                                       WHEN s.family = t.family THEN 3
                                       WHEN s.category_id = g.category_id THEN 4
                                   END * 10 + s.ord)
                        FROM seeds s) AS closeness,
                       (SELECT coalesce(i.medium_url, i.url)
                        FROM plant_images i
                        WHERE i.plant_id = p.id AND i.rejected_at IS NULL
                        ORDER BY CASE WHEN i.kind = {photo} THEN 0 ELSE 1 END, i.created_at
                        LIMIT 1) AS image_url
                FROM plants p
                LEFT JOIN plant_groups g ON g.id = p.plant_group_id
                LEFT JOIN taxonomies t ON t.id = p.taxonomy_id
                WHERE p.status = {published} AND NOT (p.id = ANY({ids}))
            )
            SELECT id, display_name AS name, plant_group, image_url
            FROM related
            WHERE closeness IS NOT NULL
            ORDER BY closeness, (image_url IS NULL), display_name, id
            OFFSET {offset} LIMIT {limit}
            """).ToListAsync(cancellationToken);

        return new ExplorePage(
            rows.Take(pageSize).Select(r => new ExploreCard(r.Id, r.Name, r.PlantGroup, r.ImageUrl)).ToList(),
            rows.Count > pageSize);
    }

    private sealed class RelatedRow
    {
        public Guid Id { get; init; }
        public required string Name { get; init; }
        public string? PlantGroup { get; init; }
        public string? ImageUrl { get; init; }
    }

    private sealed class MatchRow
    {
        public int CandidateIndex { get; init; }
        public Guid Id { get; init; }
        public required string Name { get; init; }
        public string? PlantGroup { get; init; }
        public string? PlantGroupSlug { get; init; }
        public required string ScientificName { get; init; }
        public string? ImageUrl { get; init; }
    }
}
