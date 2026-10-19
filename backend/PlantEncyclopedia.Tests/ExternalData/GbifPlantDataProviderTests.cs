using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Infrastructure;
using PlantEncyclopedia.Infrastructure.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;
using PlantEncyclopedia.Infrastructure.ExternalData.Images;
using PlantEncyclopedia.Tests.Api;

namespace PlantEncyclopedia.Tests.ExternalData;

/// <summary>
/// The GBIF adapter against recorded responses (Fixtures/Gbif) and simulated failures. Offline: every
/// request is answered by <see cref="StubHttpMessageHandler"/>.
/// </summary>
public class GbifPlantDataProviderTests
{
    // Protects against: querying the frozen Backbone (no checklistKey), plain HTTP, another host,
    // a badly escaped name, or an anonymous User-Agent.
    [Fact]
    public async Task Match_SendsOneGetToGbifV2_WithChecklistAndProjectUserAgent()
    {
        var handler = StubHttpMessageHandler.Returning(() => GbifFixtures.Response(GbifFixtures.MenthaHybrid));

        await GbifFixtures.Provider(handler).MatchAsync("Mentha × piperita", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://api.gbif.org/v2/species/match?scientificName=Mentha%20%C3%97%20piperita" +
            $"&checklistKey={GbifFixtures.ChecklistKey}&kingdom=Plantae",
            request.RequestUri!.AbsoluteUri);

        var userAgent = request.Headers.UserAgent.ToString();
        Assert.StartsWith("PlantEncyclopedia/", userAgent);
        Assert.EndsWith($"(+{GbifFixtures.Contact})", userAgent);
    }

    // Protects against: losing classification data, or storing the name with its authorship.
    [Fact]
    public async Task Match_MapsAcceptedSpecies()
    {
        var result = await MatchFixtureAsync(GbifFixtures.RosaCanina);

        Assert.Equal(TaxonLookupStatus.Found, result.Status);
        Assert.Equal(
            new ExternalTaxon(
                Provider: "gbif-col-xr",
                ExternalId: "4TCZM",
                ScientificName: "Rosa canina",
                Rank: TaxonRank.Species,
                IsExactMatch: true,
                IsAccepted: true,
                Classification: new TaxonClassification(
                    Kingdom: "Plantae",
                    Phylum: "Tracheophyta",
                    Class: "Magnoliopsida",
                    Order: "Rosales",
                    Family: "Rosaceae",
                    Genus: "Rosa",
                    Species: "Rosa canina")),
            result.Taxon);
    }

    // Protects against: using canonicalName, which drops the hybrid sign ("Mentha piperita") and the
    // rank marker ("Brassica oleracea capitata"), so the stored identity would differ from the name.
    [Theory]
    [InlineData(GbifFixtures.MenthaHybrid, "Mentha × piperita", TaxonRank.Species, "Mentha piperita")]
    [InlineData(GbifFixtures.BrassicaVariety, "Brassica oleracea var. capitata", TaxonRank.Variety, "Brassica oleracea")]
    public async Task Match_KeepsHybridSignAndRankMarker(string fixture, string expectedName, TaxonRank rank, string species)
    {
        var result = await MatchFixtureAsync(fixture);

        Assert.Equal(expectedName, result.Taxon!.ScientificName);
        Assert.Equal(rank, result.Taxon.Rank);
        Assert.Equal(species, result.Taxon.Classification.Species);
    }

    // Protects against: importing fuzzy matches, higher-rank fallbacks, synonyms or animals (ARCHITECTURE §22).
    // Each recorded answer must end in the documented decision.
    [Theory]
    [InlineData(GbifFixtures.RosaCanina, null)]
    [InlineData(GbifFixtures.MenthaHybrid, null)]
    [InlineData(GbifFixtures.BrassicaVariety, null)]
    [InlineData(GbifFixtures.RosaCaninnaFuzzy, TaxonRejection.NotExactMatch)]
    [InlineData(GbifFixtures.HomoSapiensWithPlantaeHint, TaxonRejection.NotExactMatch)]
    [InlineData(GbifFixtures.RosaGenus, TaxonRejection.RankNotAllowed)]
    [InlineData(GbifFixtures.LycopersiconSynonym, TaxonRejection.NotAccepted)]
    [InlineData(GbifFixtures.HomoSapiens, TaxonRejection.NotPlantae)]
    public async Task RecordedMatches_AreAcceptedOrRejectedAsDocumented(string fixture, TaxonRejection? expected)
    {
        var result = await MatchFixtureAsync(fixture);

        Assert.Equal(TaxonLookupStatus.Found, result.Status);
        Assert.Equal(expected, ExternalTaxonPolicy.Evaluate(result.Taxon!, out _));
    }

    // Protects against: treating "no match" as a provider failure, or a failure as "no match".
    [Theory]
    [InlineData("""{"diagnostics":{"matchType":"NONE"}}""")]
    [InlineData("""{"usage":null,"diagnostics":{"matchType":"NONE"}}""")]
    [InlineData("{}")]
    public async Task Match_ReturnsNotFound_WhenNothingMatches(string json)
    {
        var handler = StubHttpMessageHandler.Returning(() => GbifFixtures.Json(json));

        var result = await GbifFixtures.Provider(handler).MatchAsync("Nothing here", CancellationToken.None);

        Assert.Equal(TaxonLookupStatus.NotFound, result.Status);
    }

    // Protects against: storing a name that still contains its authorship (or a truncated name)
    // when the authorship is not a plain suffix of the name.
    [Fact]
    public async Task Match_ReturnsNotFound_WhenAuthorshipIsNotASuffix()
    {
        var json = GbifFixtures.Read(GbifFixtures.RosaCanina).Replace("\"authorship\":\"L.\"", "\"authorship\":\"Mill.\"");
        var handler = StubHttpMessageHandler.Returning(() => GbifFixtures.Json(json));

        var result = await GbifFixtures.Provider(handler).MatchAsync("Rosa canina", CancellationToken.None);

        Assert.Equal(TaxonLookupStatus.NotFound, result.Status);
    }

    public static TheoryData<string, Func<HttpResponseMessage>> FailedResponses => new()
    {
        { "server error", () => new HttpResponseMessage(HttpStatusCode.InternalServerError) },
        { "rate limited", () => new HttpResponseMessage(HttpStatusCode.TooManyRequests) },
        { "not found", () => new HttpResponseMessage(HttpStatusCode.NotFound) },
        { "redirect", () => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://example.org/") } } },
        { "html", () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html></html>", Encoding.UTF8, "text/html") } },
        { "invalid json", () => GbifFixtures.Json("{\"usage\": [") },
        { "over 1 MB", () => GbifFixtures.Json("{\"padding\":\"" + new string('a', GbifPlantDataProvider.MaxResponseBytes) + "\"}") }
    };

    // Protects against: provider errors surfacing as 500s, a redirect being followed to another host,
    // or an oversized body being read into memory. Every failure is Unavailable (not NotFound), so
    // 11c will not cache it as "no match".
    [Theory]
    [MemberData(nameof(FailedResponses))]
    public async Task Match_ReturnsUnavailable_WhenResponseIsUnusable(string scenario, Func<HttpResponseMessage> response)
    {
        var handler = StubHttpMessageHandler.Returning(response);

        var result = await GbifFixtures.Provider(handler).MatchAsync("Rosa canina", CancellationToken.None);

        Assert.True(result.Status == TaxonLookupStatus.Unavailable, scenario);
        Assert.Single(handler.Requests); // no retries
    }

    // Protects against: a network failure escaping as an exception.
    [Fact]
    public async Task Match_ReturnsUnavailable_WhenNetworkFails()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException(HttpRequestError.NameResolutionError, "no such host"));

        var result = await GbifFixtures.Provider(handler).MatchAsync("Rosa canina", CancellationToken.None);

        Assert.Equal(TaxonLookupStatus.Unavailable, result.Status);
    }

    // Protects against: a slow provider holding the request open beyond the 3-second limit.
    [Fact]
    public async Task Match_ReturnsUnavailable_WhenProviderIsSlow()
    {
        Assert.Equal(TimeSpan.FromSeconds(3), GbifPlantDataProvider.Timeout);

        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });

        var started = DateTime.UtcNow;
        var result = await GbifFixtures.Provider(handler).MatchAsync("Rosa canina", CancellationToken.None);

        Assert.Equal(TaxonLookupStatus.Unavailable, result.Status);
        Assert.InRange(DateTime.UtcNow - started, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(10));
    }

    // Protects against: hiding the caller's cancellation (e.g. the client disconnected) as Unavailable.
    [Fact]
    public async Task Match_Throws_WhenCallerCancels()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            GbifFixtures.Provider(handler).MatchAsync("Rosa canina", cancellation.Token));
    }

    // Protects against: the searched name reaching the logs, either from the adapter's warnings or from
    // HttpClientFactory's default request logging. That logging redacts query strings by default, but the
    // redaction can be switched off, so the registration removes it entirely. Uses the real registration.
    [Fact]
    public async Task RegisteredClient_NeverLogsTheName()
    {
        var marker = $"Markerus {Guid.NewGuid():N}";
        var logs = new CapturingLoggerProvider();
        var handler = StubHttpMessageHandler.Returning(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var services = new ServiceCollection()
            .AddLogging(b => b.AddProvider(logs).SetMinimumLevel(LogLevel.Trace))
            .AddExternalPlantData(GbifFixtures.Options(), new GenusPlantGroupMap(null), new PlantImageOptions());
        services.ConfigureAll<HttpClientFactoryOptions>(o =>
            o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = handler));
        await using var provider = services.BuildServiceProvider();

        var gbif = provider.GetRequiredService<IPlantDataProvider>();
        Assert.IsType<GuardedPlantDataProvider>(gbif);
        var result = await gbif.MatchAsync(marker, CancellationToken.None);

        Assert.Equal(TaxonLookupStatus.Unavailable, result.Status);
        Assert.Contains(marker.Split(' ')[1], Assert.Single(handler.Requests).RequestUri!.Query);
        Assert.Contains(logs.Entries, e => e.Contains("GBIF match returned HTTP 500"));
        Assert.DoesNotContain(logs.Entries, e => e.Contains(marker.Split(' ')[1]));
        Assert.DoesNotContain(logs.Entries, e => e.Contains("System.Net.Http.HttpClient"));

        // The ChecklistBank client is registered the same way.
        var taxon = ExternalTaxa.Species("Rosa", "canina", "4TCZM") with { Provider = KnownProviders.GbifCatalogueOfLife };
        Assert.Empty(await gbif.GetCommonNamesAsync(taxon, CancellationToken.None));
        Assert.Equal("api.checklistbank.org", handler.Requests[^1].RequestUri!.Host);
        Assert.Contains(logs.Entries, e => e.Contains("ChecklistBank common names returned HTTP 500"));
        Assert.DoesNotContain(logs.Entries, e => e.Contains("System.Net.Http.HttpClient"));
    }

    private static Task<TaxonLookupResult> MatchFixtureAsync(string fixture) =>
        GbifFixtures.Provider(StubHttpMessageHandler.Returning(() => GbifFixtures.Response(fixture)))
            .MatchAsync("recorded", CancellationToken.None);
}
