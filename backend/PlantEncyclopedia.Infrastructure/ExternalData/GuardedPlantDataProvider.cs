using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;

namespace PlantEncyclopedia.Infrastructure.ExternalData;

/// <summary>
/// Puts the negative cache and the app-wide call budget in front of the GBIF adapter. A cached answer
/// costs no budget; when the budget is used up the lookup is Unavailable. The name is never logged.
/// </summary>
internal sealed partial class GuardedPlantDataProvider(
    GbifPlantDataProvider inner,
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

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "External plant lookup skipped: the app-wide limit ({PerMinute}/minute, {PerDay}/day) is used up.")]
    private static partial void LogBudgetExhausted(ILogger logger, int perMinute, int perDay);
}
