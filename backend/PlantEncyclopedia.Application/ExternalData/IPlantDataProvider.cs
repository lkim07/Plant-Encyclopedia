namespace PlantEncyclopedia.Application.ExternalData;

/// <summary>
/// An external taxonomy source (ARCHITECTURE §21). Implementations live in Infrastructure, keep their
/// own response types private, and never throw for provider failures: they return
/// <see cref="TaxonLookupStatus.Unavailable"/> instead. Only cancellation by the caller is thrown.
/// </summary>
public interface IPlantDataProvider
{
    /// <summary>Looks up the best match for a scientific name. Whether it is usable is decided by <see cref="ExternalTaxonPolicy"/>.</summary>
    Task<TaxonLookupResult> MatchAsync(string scientificName, CancellationToken cancellationToken);
}

public enum TaxonLookupStatus
{
    Found,
    NotFound,
    Unavailable
}

public sealed record TaxonLookupResult(TaxonLookupStatus Status, ExternalTaxon? Taxon)
{
    public static TaxonLookupResult NotFound { get; } = new(TaxonLookupStatus.NotFound, null);
    public static TaxonLookupResult Unavailable { get; } = new(TaxonLookupStatus.Unavailable, null);
    public static TaxonLookupResult Found(ExternalTaxon taxon) => new(TaxonLookupStatus.Found, taxon);
}

public enum TaxonRank
{
    Species,
    Subspecies,
    Variety,
    Other
}

/// <summary>
/// A provider's match, translated into provider-neutral terms. Values are untrusted until
/// <see cref="ExternalTaxonPolicy"/> has accepted them.
/// </summary>
/// <param name="Provider">Stable provider name stored in plant_sources.provider, e.g. "gbif-col-xr".</param>
/// <param name="ExternalId">The provider's taxon ID.</param>
/// <param name="ScientificName">The name without authorship, keeping hybrid signs and rank markers ("Mentha × piperita", "Brassica oleracea var. capitata").</param>
/// <param name="IsExactMatch">The provider matched the name exactly (no fuzzy match, no higher-rank fallback).</param>
/// <param name="IsAccepted">The name is the accepted name, not a synonym or doubtful name.</param>
public sealed record ExternalTaxon(
    string Provider,
    string ExternalId,
    string ScientificName,
    TaxonRank Rank,
    bool IsExactMatch,
    bool IsAccepted,
    TaxonClassification Classification);

/// <summary>Higher ranks from the provider's classification; <see cref="Species"/> is the full binomial.</summary>
public sealed record TaxonClassification(
    string? Kingdom = null,
    string? Phylum = null,
    string? Class = null,
    string? Order = null,
    string? Family = null,
    string? Genus = null,
    string? Species = null);
