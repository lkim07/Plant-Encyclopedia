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

/// <summary>JSON contract for one GET /api/search/suggestions item (API_SPEC §26).</summary>
public sealed record SearchSuggestionResponse(string Text, string Type)
{
    public static SearchSuggestionResponse From(SearchSuggestion suggestion) => new(
        suggestion.Text,
        suggestion.Type switch
        {
            SuggestionType.Plant => "plant",
            SuggestionType.Cultivar => "cultivar",
            SuggestionType.ScientificName => "scientificName",
            SuggestionType.CommonName => "commonName",
            SuggestionType.Synonym => "synonym",
            SuggestionType.PlantGroup => "plantGroup",
            _ => throw new ArgumentOutOfRangeException(nameof(suggestion), suggestion.Type, "Unknown suggestion type.")
        });
}
