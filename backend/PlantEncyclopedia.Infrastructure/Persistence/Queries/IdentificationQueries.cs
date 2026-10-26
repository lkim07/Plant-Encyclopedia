using Microsoft.EntityFrameworkCore;
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
