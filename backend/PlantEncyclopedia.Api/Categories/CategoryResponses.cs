using PlantEncyclopedia.Application.Categories;

namespace PlantEncyclopedia.Api.Categories;

// JSON contracts for the category endpoints (API_SPEC §17–18). `Id` is the slug.
// Category `ImageUrl` is always null until category images exist. A plant group's `ImageUrl` is one of its plants'
// images, changing once a day (API_SPEC §18).

public sealed record CategoryResponse(string Id, string Name, string? ImageUrl)
{
    public static CategoryResponse From(CategorySummary category) => new(category.Slug, category.Name, ImageUrl: null);
}

public sealed record CategoryDetailResponse(string Id, string Name, IReadOnlyList<PlantGroupCardResponse> PlantGroups)
{
    public static CategoryDetailResponse From(CategoryDetail category) => new(
        category.Slug,
        category.Name,
        category.PlantGroups.Select(g => new PlantGroupCardResponse(g.Slug, g.Name, g.ImageUrl)).ToList());
}

public sealed record PlantGroupCardResponse(string Id, string Name, string? ImageUrl);
