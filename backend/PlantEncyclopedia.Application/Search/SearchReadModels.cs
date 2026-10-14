namespace PlantEncyclopedia.Application.Search;

/// <summary>One page of search results, best matches first.</summary>
public sealed record SearchPage(IReadOnlyList<SearchResult> Results, bool HasNext);

public sealed record SearchResult(Guid Id, string Name, string? PlantGroupName, string ScientificName);
