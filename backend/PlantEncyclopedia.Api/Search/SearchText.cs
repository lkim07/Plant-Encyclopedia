using System.Globalization;
using System.Text;

namespace PlantEncyclopedia.Api.Search;

/// <summary>
/// Validates and normalizes user search text before any database work (SECURITY §8).
/// </summary>
public static class SearchText
{
    public const int MaxLength = 100;
    public const int MaxWords = 10;

    // Longer raw input is rejected before normalization, which can expand text.
    private const int MaxRawLength = MaxLength * 4;

    public const string RequiredMessage = "Search text is required.";
    public const string TooLongMessage = "Search text must be at most 100 characters.";
    public const string ControlCharacterMessage = "Search text must not contain control characters.";
    public const string InvalidCharacterMessage = "Search text contains invalid characters.";
    public const string NoLetterOrDigitMessage = "Search text must contain a letter or digit.";
    public const string TooManyWordsMessage = "Search text must have at most 10 words.";

    /// <summary>
    /// Returns the normalized text (Unicode NFKC, trimmed), or the reason it is invalid.
    /// NFKC turns full-width and compatibility characters into their plain forms ("ｒｏｓｅ" → "rose").
    /// </summary>
    public static (string? Text, string? Error) Validate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, RequiredMessage);
        }

        if (raw.Length > MaxRawLength)
        {
            return (null, TooLongMessage);
        }

        // Control characters (including NUL, which PostgreSQL rejects in text) are never part of a name.
        if (raw.Any(char.IsControl))
        {
            return (null, ControlCharacterMessage);
        }

        string text;
        try
        {
            text = raw.Normalize(NormalizationForm.FormKC).Trim();
        }
        catch (ArgumentException)
        {
            // Invalid UTF-16, such as an unpaired surrogate.
            return (null, InvalidCharacterMessage);
        }

        if (text.Length > MaxLength)
        {
            return (null, TooLongMessage);
        }

        if (!text.Any(char.IsLetterOrDigit))
        {
            return (null, NoLetterOrDigitMessage);
        }

        if (CountWords(text) > MaxWords)
        {
            return (null, TooManyWordsMessage);
        }

        return (text, null);
    }

    /// <summary>Counts runs of letters, digits and combining marks; everything else separates words.</summary>
    private static int CountWords(string text)
    {
        var words = 0;
        var inWord = false;
        foreach (var c in text)
        {
            var isWordChar = char.IsLetterOrDigit(c) || char.GetUnicodeCategory(c) is
                UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
            if (isWordChar && !inWord)
            {
                words++;
            }

            inWord = isWordChar;
        }

        return words;
    }
}
