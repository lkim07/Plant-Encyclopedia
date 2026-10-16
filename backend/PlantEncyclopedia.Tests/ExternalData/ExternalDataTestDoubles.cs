using System.Text;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;
using PlantEncyclopedia.Tests.Api;

namespace PlantEncyclopedia.Tests.ExternalData;

/// <summary>Answers HTTP requests in memory and records them. No request ever leaves the process.</summary>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttpMessageHandler Returning(Func<HttpResponseMessage> response) =>
        new((_, _) => Task.FromResult(response()));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return respond(request, cancellationToken);
    }
}

/// <summary>Recorded GBIF responses in Fixtures/Gbif (see the README there).</summary>
internal static class GbifFixtures
{
    public const string RosaCanina = "match-rosa-canina.json";
    public const string RosaCaninnaFuzzy = "match-rosa-caninna-fuzzy.json";
    public const string RosaGenus = "match-rosa-genus.json";
    public const string HomoSapiensWithPlantaeHint = "match-homo-sapiens-plantae.json";
    public const string HomoSapiens = "match-homo-sapiens.json";
    public const string LycopersiconSynonym = "match-lycopersicon-esculentum-synonym.json";
    public const string MenthaHybrid = "match-mentha-x-piperita-hybrid.json";
    public const string BrassicaVariety = "match-brassica-oleracea-var-capitata.json";

    public const string ChecklistKey = "7ddf754f-d193-4cc9-b351-99906754a03b";
    public const string Contact = "https://github.com/lkim07/Plant-Encyclopedia";

    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gbif", name));

    public static HttpResponseMessage Json(string json) =>
        new(System.Net.HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Response(string fixture) => Json(Read(fixture));

    public static GbifOptions Options() => new() { Enabled = true, ChecklistKey = ChecklistKey, Contact = Contact };

    /// <summary>The real adapter, configured as in production, talking to <paramref name="handler"/>.</summary>
    public static GbifPlantDataProvider Provider(HttpMessageHandler handler, CapturingLoggerProvider? logs = null)
    {
        var options = Options();
        var client = new HttpClient(handler);
        GbifPlantDataProvider.ConfigureClient(client, options);

        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(logs ?? new CapturingLoggerProvider()));
        return new GbifPlantDataProvider(client, options, loggerFactory.CreateLogger<GbifPlantDataProvider>());
    }
}

/// <summary>A provider with scripted answers, counting calls.</summary>
internal sealed class FakePlantDataProvider(Func<string, CancellationToken, Task<TaxonLookupResult>> match) : IPlantDataProvider
{
    private int _calls;

    public FakePlantDataProvider(TaxonLookupResult result) : this((_, _) => Task.FromResult(result))
    {
    }

    public int Calls => _calls;

    public Task<TaxonLookupResult> MatchAsync(string scientificName, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return match(scientificName, cancellationToken);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal static class ExternalTaxa
{
    /// <summary>An exact, accepted Plantae species, as a provider would return it.</summary>
    public static ExternalTaxon Species(string genus, string epithet = "vulgaris", string? externalId = null) => new(
        Provider: "test-provider",
        ExternalId: externalId ?? Guid.NewGuid().ToString("N"),
        ScientificName: $"{genus} {epithet}",
        Rank: TaxonRank.Species,
        IsExactMatch: true,
        IsAccepted: true,
        Classification: new TaxonClassification(
            Kingdom: "Plantae",
            Phylum: "Tracheophyta",
            Class: "Magnoliopsida",
            Order: "Rosales",
            Family: "Rosaceae",
            Genus: genus,
            Species: $"{genus} {epithet}"));
}
