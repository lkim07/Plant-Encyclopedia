using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using PlantEncyclopedia.Application.ExternalData;

namespace PlantEncyclopedia.Infrastructure.ExternalData;

/// <summary>
/// Remembers provider answers that cannot lead to an import (no match, or a match the import policy
/// rejects) for 24 hours, so repeating a search does not call the provider again (ARCHITECTURE §24).
/// Unavailable is never stored: a failure must not look like "no such plant". Successful matches are not
/// stored either, because the imported plant is then found in the database. In memory, at most 10,000
/// names, keyed by the exact name sent to the provider.
/// </summary>
internal sealed class NegativeLookupCache : IDisposable
{
    public const int MaxEntries = 10_000;
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private readonly MemoryCache _cache;

    public NegativeLookupCache(TimeProvider timeProvider)
    {
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = MaxEntries,
            Clock = new TimeProviderClock(timeProvider)
        });
    }

    public bool TryGet(string name, out TaxonLookupResult result) =>
        _cache.TryGetValue(name, out result!);

    /// <summary>Stores the answer only if it can never lead to an import.</summary>
    public void StoreIfNegative(string name, TaxonLookupResult result)
    {
        var negative = result.Status == TaxonLookupStatus.NotFound
            || (result is { Status: TaxonLookupStatus.Found, Taxon: { } taxon }
                && ExternalTaxonPolicy.Evaluate(taxon, out _) is not null);

        if (negative)
        {
            _cache.Set(name, result, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = Lifetime });
        }
    }

    public void Dispose() => _cache.Dispose();

    private sealed class TimeProviderClock(TimeProvider timeProvider) : ISystemClock
    {
        public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    }
}
