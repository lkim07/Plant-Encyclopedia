using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData;

namespace PlantEncyclopedia.Tests.ExternalData;

public class ExternalLookupNameTests
{
    // Protects against: sending common phrases, numbers or long text to GBIF, and botanical casing
    // not being applied (GBIF matches "Rosa canina").
    [Theory]
    [InlineData("Rosa canina", "Rosa canina")]
    [InlineData("rosa CANINA", "Rosa canina")]
    [InlineData("  rosa   canina ", "Rosa canina")]
    [InlineData("Mentha × piperita", "Mentha × piperita")]
    [InlineData("mentha x piperita", "Mentha x piperita")]
    [InlineData("Brassica oleracea var. capitata", "Brassica oleracea var. capitata")]
    [InlineData("Capsella bursa-pastoris", "Capsella bursa-pastoris")]
    [InlineData("peace rose", "Peace rose")]
    [InlineData("rose", null)]
    [InlineData("a b c d e", null)]
    [InlineData("rose 2024", null)]
    [InlineData("Rosa 'Peace'", null)]
    [InlineData("× Citrofortunella microcarpa", null)]
    [InlineData("rosa (canina)", null)]
    public void TryPrepare(string text, string? expected) =>
        Assert.Equal(expected, ExternalLookupName.TryPrepare(text));
}

public class ExternalCallBudgetTests
{
    // Protects against: one busy period (or one abusive client population) exhausting GBIF's goodwill.
    [Fact]
    public void AllowsThirtyCallsPerMinute()
    {
        var time = new ManualTimeProvider();
        var budget = new ExternalCallBudget(time);

        for (var i = 0; i < ExternalCallBudget.PerMinute; i++) Assert.True(budget.TryAcquire());
        Assert.False(budget.TryAcquire());

        time.Advance(TimeSpan.FromSeconds(59));
        Assert.False(budget.TryAcquire());

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(budget.TryAcquire());
    }

    [Fact]
    public void AllowsOneThousandCallsPerDay()
    {
        var time = new ManualTimeProvider();
        var budget = new ExternalCallBudget(time);

        var granted = 0;
        for (var minute = 0; minute < 60; minute++)
        {
            while (budget.TryAcquire()) granted++;
            time.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Equal(ExternalCallBudget.PerDay, granted);

        time.Advance(TimeSpan.FromHours(23));
        Assert.True(budget.TryAcquire());
    }
}

/// <summary>The negative cache and call budget in front of the real GBIF adapter, on recorded responses.</summary>
public class GuardedPlantDataProviderTests
{
    private readonly ManualTimeProvider _time = new();

    // Protects against: repeating a hopeless search calling GBIF again within 24 hours.
    [Theory]
    [InlineData(GbifFixtures.RosaCaninnaFuzzy)]
    [InlineData(GbifFixtures.LycopersiconSynonym)]
    [InlineData(GbifFixtures.HomoSapiens)]
    public async Task RejectedMatches_AreCachedFor24Hours(string fixture)
    {
        var (provider, handler) = Create(() => GbifFixtures.Response(fixture));

        var first = await provider.MatchAsync("Rosa caninna", CancellationToken.None);
        var second = await provider.MatchAsync("Rosa caninna", CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Single(handler.Requests);

        _time.Advance(NegativeLookupCache.Lifetime);
        await provider.MatchAsync("Rosa caninna", CancellationToken.None);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task NoMatch_IsCached()
    {
        var (provider, handler) = Create(() => GbifFixtures.Json("""{"diagnostics":{"matchType":"NONE"}}"""));

        await provider.MatchAsync("Nothing here", CancellationToken.None);
        var second = await provider.MatchAsync("Nothing here", CancellationToken.None);

        Assert.Equal(TaxonLookupStatus.NotFound, second.Status);
        Assert.Single(handler.Requests);
    }

    // Protects against: an outage being remembered as "no such plant" for a day.
    [Fact]
    public async Task Unavailable_IsNeverCached()
    {
        var (provider, handler) = Create(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await provider.MatchAsync("Rosa canina", CancellationToken.None);
        await provider.MatchAsync("Rosa canina", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    // Importable matches are not cached: the imported plant is found in the database next time.
    [Fact]
    public async Task AcceptedMatch_IsNotCached()
    {
        var (provider, handler) = Create(() => GbifFixtures.Response(GbifFixtures.RosaCanina));

        await provider.MatchAsync("Rosa canina", CancellationToken.None);
        await provider.MatchAsync("Rosa canina", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Cache_IsKeyedByExactName()
    {
        var (provider, handler) = Create(() => GbifFixtures.Response(GbifFixtures.RosaCaninnaFuzzy));

        await provider.MatchAsync("Rosa caninna", CancellationToken.None);
        await provider.MatchAsync("Rosa canninna", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    // Protects against: exceeding the app-wide GBIF budget; cached answers must not use it up.
    [Fact]
    public async Task ExhaustedBudget_ReturnsUnavailable_WithoutCallingGbif()
    {
        var (provider, handler) = Create(() => GbifFixtures.Response(GbifFixtures.RosaCanina));

        for (var i = 0; i < ExternalCallBudget.PerMinute; i++)
        {
            Assert.Equal(TaxonLookupStatus.Found, (await provider.MatchAsync("Rosa canina", CancellationToken.None)).Status);
        }

        var result = await provider.MatchAsync("Rosa canina", CancellationToken.None);

        Assert.Equal(TaxonLookupStatus.Unavailable, result.Status);
        Assert.Equal(ExternalCallBudget.PerMinute, handler.Requests.Count);
    }

    [Fact]
    public async Task CachedAnswers_DoNotUseTheBudget()
    {
        var (provider, handler) = Create(() => GbifFixtures.Response(GbifFixtures.RosaCaninnaFuzzy));

        for (var i = 0; i < ExternalCallBudget.PerMinute + 10; i++)
        {
            Assert.Equal(TaxonLookupStatus.Found, (await provider.MatchAsync("Rosa caninna", CancellationToken.None)).Status);
        }

        Assert.Single(handler.Requests);
    }

    private (GuardedPlantDataProvider Provider, StubHttpMessageHandler Handler) Create(Func<HttpResponseMessage> response)
    {
        var handler = StubHttpMessageHandler.Returning(response);
        var provider = new GuardedPlantDataProvider(
            GbifFixtures.Provider(handler),
            new NegativeLookupCache(_time),
            new ExternalCallBudget(_time),
            NullLogger<GuardedPlantDataProvider>.Instance);
        return (provider, handler);
    }
}
