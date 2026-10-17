using System.Text.RegularExpressions;

namespace PlantEncyclopedia.Application.ExternalData;

/// <summary>
/// Decides whether search text is worth sending to an external taxonomy provider (API_SPEC §27).
/// Only text shaped like a scientific name qualifies: 2–4 words of letters, where a word may also
/// contain "." or "-" ("var.", "bursa-pastoris") or be the hybrid sign "×". Common names such as
/// "peace rose" still pass, but digits, quotes and longer phrases do not, which saves provider calls.
/// </summary>
public static partial class ExternalLookupName
{
    /// <summary>
    /// Returns the name in botanical casing (genus capitalized, everything else lower-case, as in
    /// "Rosa canina"), or null when the text is not name-shaped.
    /// </summary>
    /// <param name="text">Search text already validated and normalized by the caller.</param>
    public static string? TryPrepare(string text)
    {
        var words = Whitespace().Split(text.Trim());
        if (words.Length is < 2 or > 4
            || !char.IsLetter(words[0][0])
            || !words.All(w => WordPattern().IsMatch(w)))
        {
            return null;
        }

        var name = string.Join(' ', words).ToLowerInvariant();
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^(?:×|\p{L}[\p{L}\p{M}.\-]*)$")]
    private static partial Regex WordPattern();
}
