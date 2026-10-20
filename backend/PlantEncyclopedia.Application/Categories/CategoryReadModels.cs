namespace PlantEncyclopedia.Application.Categories;

/// <summary>A category in the category list. Not a database entity.</summary>
public sealed record CategorySummary(string Slug, string Name);

/// <summary>A category with all of its plant groups (category-pure).</summary>
public sealed record CategoryDetail(string Slug, string Name, IReadOnlyList<PlantGroupSummary> PlantGroups);

/// <summary>A plant group as listed on its category page.</summary>
/// <param name="ImageUrl">The card image: a usable image of one of the group's Published plants (photos first), the same
/// plant all day and a different one on another day; null when none of its plants has an image.</param>
public sealed record PlantGroupSummary(string Slug, string Name, string? ImageUrl = null);
