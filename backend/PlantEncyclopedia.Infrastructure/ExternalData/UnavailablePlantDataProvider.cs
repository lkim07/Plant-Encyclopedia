using PlantEncyclopedia.Application.ExternalData;

namespace PlantEncyclopedia.Infrastructure.ExternalData;

/// <summary>Used when no external provider is enabled (the default, and always in tests): every lookup is Unavailable.</summary>
internal sealed class UnavailablePlantDataProvider : IPlantDataProvider
{
    public Task<TaxonLookupResult> MatchAsync(string scientificName, CancellationToken cancellationToken) =>
        Task.FromResult(TaxonLookupResult.Unavailable);
}
