using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData.ChecklistBank;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;

namespace PlantEncyclopedia.Infrastructure.ExternalData;

/// <summary>
/// Puts the negative cache and the app-wide call budget in front of the GBIF adapter, and adds common
/// names from ChecklistBank. A cached answer costs no budget; when the budget is used up the lookup is
/// Unavailable and common names are skipped. Every external request, GBIF or ChecklistBank, takes one
/// call from the same budget. The name is never logged.
/// </summary>
internal sealed partial class GuardedPlantDataProvider(
    GbifPlantDataProvider inner,
    ChecklistBankCommonNameClient commonNames,
    NegativeLookupCache cache,
    ExternalCallBudget budget,
    ILogger<GuardedPlantDataProvider> logger) : IPlantDataProvider
{
    public async Task<TaxonLookupResult> MatchAsync(string scientificName, CancellationToken cancellationToken)
    {
        if (cache.TryGet(scientificName, out var cached))
        {
            return cached;
        }

        if (!budget.TryAcquire())
        {
            LogBudgetExhausted(logger, ExternalCallBudget.PerMinute, ExternalCallBudget.PerDay);
            return TaxonLookupResult.Unavailable;
        }

        var result = await inner.MatchAsync(scientificName, cancellationToken);
        cache.StoreIfNegative(scientificName, result);
        return result;
    }

    public async Task<IReadOnlyList<ExternalCommonName>> GetCommonNamesAsync(ExternalTaxon taxon, CancellationToken cancellationToken)
    {
        // ChecklistBank's dataset 3LXR uses the same IDs as GBIF's COL XR checklist, and no other provider's.
        if (taxon.Provider != KnownProviders.GbifCatalogueOfLife)
        {
            return [];
        }

        if (!budget.TryAcquire())
        {
            LogBudgetExhausted(logger, ExternalCallBudget.PerMinute, ExternalCallBudget.PerDay);
            return [];
        }

        return await commonNames.GetAsync(taxon.ExternalId, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "External plant lookup skipped: the app-wide limit ({PerMinute}/minute, {PerDay}/day) is used up.")]
    private static partial void LogBudgetExhausted(ILogger logger, int perMinute, int perDay);
}
