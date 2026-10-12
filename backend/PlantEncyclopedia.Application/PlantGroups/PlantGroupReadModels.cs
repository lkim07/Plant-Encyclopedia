namespace PlantEncyclopedia.Application.PlantGroups;

/// <summary>A plant group with one page of its Published plants. Not a database entity.</summary>
public sealed record PlantGroupDetail(
    string Slug,
    string Name,
    CategoryReference Category,
    IReadOnlyList<PlantSummary> Plants,
    bool HasNext);

/// <summary>The category a plant group belongs to.</summary>
public sealed record CategoryReference(string Slug, string Name);

/// <summary>A plant as listed on its plant-group page.</summary>
public sealed record PlantSummary(Guid Id, string Name);
