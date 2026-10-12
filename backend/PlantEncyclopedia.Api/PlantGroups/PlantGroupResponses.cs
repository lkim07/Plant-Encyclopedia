using PlantEncyclopedia.Application.PlantGroups;

namespace PlantEncyclopedia.Api.PlantGroups;

// JSON contract for GET /api/plant-groups/{plantGroupId} (API_SPEC §19).
// Group and category `Id` values are slugs; plant `Id` values are UUIDs.
// `ImageUrl` is always null until plant images exist.

public sealed record PlantGroupDetailResponse(
    string Id,
    string Name,
    CategoryReferenceResponse Category,
    IReadOnlyList<PlantCardResponse> Plants)
{
    public static PlantGroupDetailResponse From(PlantGroupDetail group) => new(
        group.Slug,
        group.Name,
        new CategoryReferenceResponse(group.Category.Slug, group.Category.Name),
        group.Plants.Select(p => new PlantCardResponse(p.Id, p.Name, ImageUrl: null)).ToList());
}

public sealed record CategoryReferenceResponse(string Id, string Name);

public sealed record PlantCardResponse(Guid Id, string Name, string? ImageUrl);
