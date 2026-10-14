using PlantEncyclopedia.Application.Search;

namespace PlantEncyclopedia.Api.Search;

/// <summary>JSON contract for one GET /api/search result (API_SPEC §25). Changing it changes the public API.</summary>
public sealed record SearchResultResponse(
    Guid Id,
    string Name,
    string? PlantGroup,
    string ScientificName,
    string? ImageUrl)
{
    public static SearchResultResponse From(SearchResult result) => new(
        result.Id,
        result.Name,
        result.PlantGroupName,
        result.ScientificName,
        ImageUrl: null); // Plant images are not served yet.
}
