using System.Text;
using System.Text.RegularExpressions;

namespace PlantEncyclopedia.Application.ExternalData;

public enum TaxonRejection
{
    NotExactMatch,
    NotAccepted,
    RankNotAllowed,
    NotPlantae,
    InvalidData
}

/// <summary>
/// Decides whether an external match may be imported (ARCHITECTURE §22): exact matches of accepted
/// Plantae names at species, subspecies or variety rank only. Fuzzy matches, higher-rank fallbacks,
/// synonyms and non-plant taxa are rejected. Text is normalized and length-checked against the
/// database columns, so a malformed response is rejected instead of failing on insert.
/// </summary>
public static partial class ExternalTaxonPolicy
{
    public const string PlantKingdom = "Plantae";

    // Column limits. A scientific name is also the display name until a common name exists, so it must
    // fit plants.display_name (200), not only plants.scientific_name (300).
    private const int ScientificNameMaxLength = 200;
    private const int ExternalIdMaxLength = 200;
    private const int RankMaxLength = 100;

    /// <summary>Returns null when the taxon is acceptable, with <paramref name="normalized"/> set.</summary>
    public static TaxonRejection? Evaluate(ExternalTaxon taxon, out ExternalTaxon normalized)
    {
        normalized = taxon;

        if (!taxon.IsExactMatch) return TaxonRejection.NotExactMatch;
        if (!taxon.IsAccepted) return TaxonRejection.NotAccepted;
        if (taxon.Rank is not (TaxonRank.Species or TaxonRank.Subspecies or TaxonRank.Variety)) return TaxonRejection.RankNotAllowed;
        if (taxon.Classification.Kingdom != PlantKingdom) return TaxonRejection.NotPlantae;

        var scientificName = NormalizeText(taxon.ScientificName);
        var classification = taxon.Classification with
        {
            Phylum = NormalizeText(taxon.Classification.Phylum),
            Class = NormalizeText(taxon.Classification.Class),
            Order = NormalizeText(taxon.Classification.Order),
            Family = NormalizeText(taxon.Classification.Family),
            Genus = NormalizeText(taxon.Classification.Genus),
            Species = NormalizeText(taxon.Classification.Species)
        };

        var valid =
            IsValidText(scientificName, ScientificNameMaxLength)
            && scientificName!.Contains(' ')
            // The full name is also stored in taxonomies.species, .subspecies or .variety.
            && scientificName.Length <= RankMaxLength
            && taxon.ExternalId.Length <= ExternalIdMaxLength
            && ExternalIdPattern().IsMatch(taxon.ExternalId)
            && IsValidText(classification.Genus, RankMaxLength)
            && scientificName.StartsWith(classification.Genus + " ", StringComparison.Ordinal)
            && IsValidOptionalRank(classification.Phylum)
            && IsValidOptionalRank(classification.Class)
            && IsValidOptionalRank(classification.Order)
            && IsValidOptionalRank(classification.Family)
            && IsValidOptionalRank(classification.Species);

        if (!valid) return TaxonRejection.InvalidData;

        normalized = taxon with { ScientificName = scientificName!, Classification = classification };
        return null;
    }

    /// <summary>
    /// Validates a scientific name supplied by a caller before any provider is asked.
    /// Returns the normalized name, or null when it is not a plausible name.
    /// </summary>
    public static string? NormalizeRequestedName(string? value)
    {
        var name = NormalizeText(value);
        return IsValidText(name, ScientificNameMaxLength) && name!.Any(char.IsLetter) ? name : null;
    }

    // NFC, whitespace runs collapsed to one space, trimmed. Null or blank stays null.
    internal static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Whitespace().Replace(value.Normalize(NormalizationForm.FormC), " ").Trim();
    }

    internal static bool IsValidText(string? value, int maxLength) =>
        value is not null && value.Length <= maxLength && !value.Any(char.IsControl);

    private static bool IsValidOptionalRank(string? value) => value is null || IsValidText(value, RankMaxLength);

    // \s in .NET covers Unicode whitespace (no-break space, ideographic space, ...).
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // Provider IDs are short opaque tokens (COL XR uses "4TCZM"); anything else is not trusted.
    [GeneratedRegex("^[A-Za-z0-9._:-]+$")]
    private static partial Regex ExternalIdPattern();
}
