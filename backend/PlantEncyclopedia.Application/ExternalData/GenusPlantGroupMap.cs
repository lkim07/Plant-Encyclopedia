using System.Text.RegularExpressions;

namespace PlantEncyclopedia.Application.ExternalData;

/// <summary>
/// The configured genus → plant group slug list (ExternalPlantData:GenusPlantGroups). An imported plant
/// can only be published automatically when its genus is listed here; ambiguous genera such as Solanum
/// (tomato, potato, eggplant) are deliberately left out, so those plants are saved as NeedsReview.
/// Invalid configuration fails at startup.
/// </summary>
public sealed partial class GenusPlantGroupMap
{
    private readonly Dictionary<string, string> _slugsByGenus;

    public GenusPlantGroupMap(IReadOnlyDictionary<string, string>? genusToSlug)
    {
        _slugsByGenus = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (genus, slug) in genusToSlug ?? new Dictionary<string, string>())
        {
            if (!GenusPattern().IsMatch(genus))
            {
                throw new ArgumentException($"Genus '{genus}' must be one capitalized word, e.g. 'Rosa'.", nameof(genusToSlug));
            }

            if (slug is null || !SlugPattern().IsMatch(slug))
            {
                throw new ArgumentException($"Plant group slug for genus '{genus}' is not a valid slug.", nameof(genusToSlug));
            }

            _slugsByGenus.Add(genus, slug);
        }
    }

    public int Count => _slugsByGenus.Count;

    /// <summary>The plant group slug for an exact, case-sensitive genus name, or null.</summary>
    public string? FindSlug(string? genus) =>
        genus is not null && _slugsByGenus.TryGetValue(genus, out var slug) ? slug : null;

    [GeneratedRegex("^[A-Z][a-z]+(-[a-z]+)?$")]
    private static partial Regex GenusPattern();

    // Same shape as the plant group slug CHECK constraint (SqlConstraints.SlugPattern).
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
