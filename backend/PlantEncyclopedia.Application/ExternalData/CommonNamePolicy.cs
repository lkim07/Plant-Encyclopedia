namespace PlantEncyclopedia.Application.ExternalData;

/// <summary>
/// A common (vernacular) name as a provider returned it. Untrusted until <see cref="CommonNamePolicy"/> has selected it.
/// </summary>
/// <param name="Name">The name as written by the source, e.g. "dog rose".</param>
/// <param name="Language">The provider's language code, e.g. "eng" (ISO 639-3) or "en".</param>
/// <param name="HasReference">The provider cites a source reference for this name.</param>
public sealed record ExternalCommonName(string Name, string? Language, bool HasReference);

/// <summary>A common name ready to be stored as a plant_names row.</summary>
public sealed record SelectedCommonName(string Name, string LanguageCode);

/// <param name="Names">Validated names in the supported languages, without case-insensitive duplicates.</param>
/// <param name="EnglishDisplayName">The English name to show with the scientific name, or null when none stands out.</param>
public sealed record SelectedCommonNames(IReadOnlyList<SelectedCommonName> Names, string? EnglishDisplayName)
{
    public static SelectedCommonNames None { get; } = new([], null);
}

/// <summary>
/// Selects the common names to import (ARCHITECTURE §22). Only the app's languages are kept (en, fr, de, ko);
/// a plant may have names in some, one or none of them. Providers do not mark a preferred name, so the
/// English display name is the one the source lists most often, counting spellings that differ only in
/// case, hyphens or spacing as the same name ("Dog-rose", "dog rose"). A tie goes to the only tied name
/// with a cited reference; if that does not settle it, no English name is shown.
/// </summary>
public static class CommonNamePolicy
{
    // plant_names.name and plants.display_name are 200 characters.
    private const int NameMaxLength = 200;

    /// <summary>Bounds the rows one import can write, whatever the provider returns.</summary>
    public const int MaxNames = 40;

    private static readonly Dictionary<string, string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = "en", ["en"] = "en",
        ["fra"] = "fr", ["fre"] = "fr", ["fr"] = "fr",
        ["deu"] = "de", ["ger"] = "de", ["de"] = "de",
        ["kor"] = "ko", ["ko"] = "ko"
    };

    public static SelectedCommonNames Select(IEnumerable<ExternalCommonName> names)
    {
        var valid = names
            .Select(n => (Name: ExternalTaxonPolicy.NormalizeText(n.Name), Language: ToLanguageCode(n.Language), n.HasReference))
            .Where(n => n.Language is not null && IsValidName(n.Name))
            .Select(n => (Name: n.Name!, Language: n.Language!, n.HasReference))
            .ToList();

        var selected = valid
            // A referenced spelling wins over an unreferenced duplicate that differs only in case.
            .OrderByDescending(n => n.HasReference)
            .DistinctBy(n => (n.Language, Key: n.Name.ToUpperInvariant()))
            .Take(MaxNames)
            .Select(n => new SelectedCommonName(n.Name, n.Language))
            .ToList();

        var english = valid.Where(n => n.Language == "en").Select(n => (n.Name, n.HasReference)).ToList();
        return new SelectedCommonNames(selected, ChooseDisplayName(english));
    }

    private static string? ChooseDisplayName(List<(string Name, bool HasReference)> english)
    {
        var groups = english.GroupBy(n => SameNameKey(n.Name)).ToList();
        if (groups.Count == 0) return null;

        var mostListed = groups.Max(g => g.Count());
        var candidates = groups.Where(g => g.Count() == mostListed).ToList();
        if (candidates.Count > 1)
        {
            candidates = candidates.Where(g => g.Any(n => n.HasReference)).ToList();
        }

        if (candidates.Count != 1) return null;

        var chosen = candidates[0].OrderByDescending(n => n.HasReference).First().Name;
        return string.Concat(chosen[..1].ToUpperInvariant(), chosen[1..]);
    }

    // "Dog-rose", "dog rose" and "Dog  Rose" are one name.
    private static string SameNameKey(string name) =>
        string.Join(' ', name.ToLowerInvariant().Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries));

    private static string? ToLanguageCode(string? language) =>
        language is not null && Languages.TryGetValue(language, out var code) ? code : null;

    private static bool IsValidName(string? name) =>
        ExternalTaxonPolicy.IsValidText(name, NameMaxLength) && name!.Any(char.IsLetter);
}
