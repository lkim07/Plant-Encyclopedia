using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using PlantEncyclopedia.Application.Identification;
using PlantEncyclopedia.Infrastructure.ExternalData;

namespace PlantEncyclopedia.Infrastructure.Identification;

/// <summary>
/// App-wide cap on identification calls (API_SPEC §40): 20 per minute and 400 per day, whoever asks, which keeps
/// usage under Pl@ntNet's free 500 per day. In memory, per API instance.
/// </summary>
internal sealed class IdentificationCallBudget(TimeProvider timeProvider)
    : ExternalCallBudget(timeProvider, IdentificationCallBudget.PerMinute, IdentificationCallBudget.PerDay)
{
    public new const int PerMinute = 20;
    public new const int PerDay = 400;
}

/// <summary>
/// Remembers the provider's suggestions for an identical photo for 24 hours (ARCHITECTURE §29), keyed by the SHA-256
/// of the prepared JPEG. Only suggested names and scores are kept, never the photo or anything about who sent it,
/// so a repeated photo is answered without a provider call and without sharing anything private. Unavailable is
/// never stored. At most <see cref="MaxEntries"/> photos.
/// </summary>
internal sealed class IdentificationCache : IDisposable
{
    public const int MaxEntries = 1_000;
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private readonly MemoryCache _cache;

    public IdentificationCache(TimeProvider timeProvider) =>
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = MaxEntries, Clock = new TimeProviderClock(timeProvider) });

    public static string KeyFor(byte[] jpeg) => Convert.ToHexString(SHA256.HashData(jpeg));

    public bool TryGet(string key, out ProviderIdentification answer) => _cache.TryGetValue(key, out answer!);

    public void Set(string key, ProviderIdentification answer)
    {
        if (answer.Status != ProviderIdentificationStatus.Answered) return;
        _cache.Set(key, answer, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = Lifetime });
    }

    public void Dispose() => _cache.Dispose();

    private sealed class TimeProviderClock(TimeProvider timeProvider) : ISystemClock
    {
        public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    }
}

/// <summary>Cache first, then the app-wide budget, then the real provider (registered when identification is enabled).</summary>
internal sealed class GuardedIdentificationProvider(
    PlantNetIdentificationProvider inner,
    IdentificationCache cache,
    IdentificationCallBudget budget) : IPlantIdentificationProvider
{
    public async Task<ProviderIdentification> IdentifyAsync(byte[] jpeg, CancellationToken cancellationToken)
    {
        var key = IdentificationCache.KeyFor(jpeg);
        if (cache.TryGet(key, out var cached)) return cached;
        if (!budget.TryAcquire()) return ProviderIdentification.Unavailable;

        var answer = await inner.IdentifyAsync(jpeg, cancellationToken);
        cache.Set(key, answer);
        return answer;
    }
}

/// <summary>Used when identification is not configured: every request is Unavailable and nothing is called.</summary>
internal sealed class UnavailableIdentificationProvider : IPlantIdentificationProvider
{
    public Task<ProviderIdentification> IdentifyAsync(byte[] jpeg, CancellationToken cancellationToken) =>
        Task.FromResult(ProviderIdentification.Unavailable);
}
