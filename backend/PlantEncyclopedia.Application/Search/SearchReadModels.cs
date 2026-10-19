namespace PlantEncyclopedia.Application.Search;

/// <summary>One page of search results, best matches first.</summary>
public sealed record SearchPage(IReadOnlyList<SearchResult> Results, bool HasNext);

/// <param name="ImageUrl">The card image: a photo if there is one, otherwise an illustration.</param>
public sealed record SearchResult(Guid Id, string Name, string? PlantGroupName, string ScientificName, string? ImageUrl);

/// <summary>A name to offer while the user types. Selecting it runs a search for <see cref="Text"/>.</summary>
public sealed record SearchSuggestion(string Text, SuggestionType Type);

/// <summary>Where a suggested name comes from. When one text has several sources, the lowest value wins.</summary>
public enum SuggestionType
{
    Plant = 1,
    Cultivar = 2,
    ScientificName = 3,
    CommonName = 4,
    Synonym = 5,
    PlantGroup = 6
}
