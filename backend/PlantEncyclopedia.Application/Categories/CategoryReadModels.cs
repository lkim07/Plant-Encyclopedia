namespace PlantEncyclopedia.Application.Categories;

/// <summary>A category in the category list. Not a database entity.</summary>
public sealed record CategorySummary(string Slug, string Name);

/// <summary>A category with all of its plant groups (category-pure).</summary>
public sealed record CategoryDetail(string Slug, string Name, IReadOnlyList<PlantGroupSummary> PlantGroups);

/// <summary>A plant group as listed on its category page.</summary>
public sealed record PlantGroupSummary(string Slug, string Name);
