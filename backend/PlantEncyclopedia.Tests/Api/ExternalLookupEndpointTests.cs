using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Tests.ExternalData;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// POST /api/search/external (API_SPEC §27) with the real ingestion and database, and a fake provider in
/// place of GBIF. Every test uses its own genus, so plants from other tests never match.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ExternalLookupEndpointTests(PostgresDatabaseFixture fixture)
{
    private const string Url = "/api/search/external";

    // Protects against: an imported plant not being returned, or the response shape drifting.
    [Fact]
    public async Task Lookup_ImportsAndReturnsPublishedPlant()
    {
        var genus = TestData.NewGenus();
        var group = await NewGroupAsync();
        var provider = new RecordingProvider(TaxonLookupResult.Found(ExternalTaxa.Species(genus)));
        await using var factory = Factory(provider, new GenusPlantGroupMap(new Dictionary<string, string> { [genus] = group.Slug }));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Url, new { q = $"{genus.ToLowerInvariant()} VULGARIS" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plantId = await SinglePlantIdAsync(genus);
        Assert.Equal(
            """{"data":{"status":"found","plant":{"id":""" + $"\"{plantId}\",\"name\":\"{genus} vulgaris\"," +
            $"\"plantGroup\":\"Test Group\",\"scientificName\":\"{genus} vulgaris\",\"imageUrl\":null" + "}}}",
            await response.Content.ReadAsStringAsync());

        // GBIF was asked in botanical casing, and the plant is now found by the ordinary search.
        Assert.Equal([$"{genus} vulgaris"], provider.Names);
        using var search = JsonDocument.Parse(await client.GetStringAsync("/api/search?q=" + genus));
        Assert.Equal(plantId, search.RootElement.GetProperty("data")[0].GetProperty("id").GetGuid());
    }

    // Protects against: revealing unreviewed imports (no group mapping → NeedsReview).
    [Fact]
    public async Task Lookup_ReturnsPendingReview_ForUnmappedImport()
    {
        var genus = TestData.NewGenus();
        await using var factory = Factory(new RecordingProvider(TaxonLookupResult.Found(ExternalTaxa.Species(genus))));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Url, new { q = $"{genus} vulgaris" });

        Assert.Equal("""{"data":{"status":"pendingReview"}}""", await response.Content.ReadAsStringAsync());
        Assert.NotEqual(Guid.Empty, await SinglePlantIdAsync(genus));
    }

    // Protects against: calling GBIF for plants already stored, or showing stored plants that are not Published.
    [Theory]
    [InlineData(PlantStatus.Published, "found")]
    [InlineData(PlantStatus.Draft, "pendingReview")]
    [InlineData(PlantStatus.NeedsReview, "pendingReview")]
    [InlineData(PlantStatus.Archived, "notFound")]
    public async Task Lookup_UsesStoredPlant_WithoutCallingProvider(PlantStatus status, string expected)
    {
        var genus = TestData.NewGenus();
        var group = await NewGroupAsync();
        await using (var db = fixture.CreateDbContext())
        {
            db.Plants.Add(new Plant { DisplayName = "Stored", ScientificName = $"{genus} vulgaris", PlantGroupId = group.Id, Status = status });
            await db.SaveChangesAsync();
        }

        var provider = new RecordingProvider(TaxonLookupResult.Unavailable);
        await using var factory = Factory(provider);
        using var client = factory.CreateClient();

        using var json = await PostAsync(client, $"{genus} vulgaris");

        Assert.Equal(expected, json.RootElement.GetProperty("data").GetProperty("status").GetString());
        Assert.Empty(provider.Names);
    }

    // Protects against: provider calls for text that cannot be a scientific name.
    [Theory]
    [InlineData("rose")]
    [InlineData("rose 2024")]
    [InlineData("Rosa 'Peace'")]
    [InlineData("one two three four five")]
    public async Task Lookup_ReturnsNotFound_ForTextThatIsNotNameShaped(string q)
    {
        var provider = new RecordingProvider(TaxonLookupResult.Unavailable);
        await using var factory = Factory(provider);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Url, new { q });

        Assert.Equal("""{"data":{"status":"notFound"}}""", await response.Content.ReadAsStringAsync());
        Assert.Empty(provider.Names);
    }

    // Protects against: rejected matches (here a fuzzy one) being imported or described to the client.
    [Fact]
    public async Task Lookup_ReturnsNotFound_ForRejectedMatch()
    {
        var genus = TestData.NewGenus();
        var fuzzy = ExternalTaxa.Species(genus) with { IsExactMatch = false };
        await using var factory = Factory(new RecordingProvider(TaxonLookupResult.Found(fuzzy)));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Url, new { q = $"{genus} vulgaris" });

        Assert.Equal("""{"data":{"status":"notFound"}}""", await response.Content.ReadAsStringAsync());
        await using var db = fixture.CreateDbContext();
        Assert.Equal(0, db.Plants.Count(p => p.ScientificName.StartsWith(genus)));
    }

    // Protects against: provider failures surfacing as 500s or as "not found" (API_SPEC §53).
    [Fact]
    public async Task Lookup_Returns503_WhenProviderUnavailable()
    {
        await using var factory = Factory(new RecordingProvider(TaxonLookupResult.Unavailable));
        using var client = factory.CreateClient();

        await AssertUnavailableAsync(await client.PostAsJsonAsync(Url, new { q = $"{TestData.NewGenus()} vulgaris" }));
    }

    // In tests GBIF is always disabled, so without a fake provider every lookup is unavailable.
    [Fact]
    public async Task Lookup_Returns503_WhenExternalDataIsDisabled()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        await AssertUnavailableAsync(await client.PostAsJsonAsync(Url, new { q = $"{TestData.NewGenus()} vulgaris" }));
    }

    public static TheoryData<string, string, string> InvalidBodies => new()
    {
        { """{"q":""}""", "application/json", """{"q":"Search text is required."}""" },
        { """{}""", "application/json", """{"q":"Search text is required."}""" },
        { "null", "application/json", """{"q":"Search text is required."}""" },
        { """{"q":"rose\u0007"}""", "application/json", """{"q":"Search text must not contain control characters."}""" },
        { """{"q":"rose""", "application/json", """{"body":"Request body must be valid JSON."}""" },
        { """{"q":5}""", "application/json", """{"body":"Request body must be valid JSON."}""" },
        { "q=rose", "application/x-www-form-urlencoded", """{"body":"Request body must be JSON."}""" }
    };

    // Protects against: malformed requests reaching the provider, or failing with a framework error format.
    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task InvalidBody_Returns400(string body, string contentType, string expectedDetails)
    {
        var provider = new RecordingProvider(TaxonLookupResult.Unavailable);
        await using var factory = Factory(provider);
        using var client = factory.CreateClient();

        var response = await client.PostAsync(Url, new StringContent(body, Encoding.UTF8, contentType));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"VALIDATION_ERROR","message":"One or more fields are invalid.","details":""" + expectedDetails + "}}",
            await response.Content.ReadAsStringAsync());
        Assert.Empty(provider.Names);
    }

    // Protects against: large bodies being read into memory.
    [Fact]
    public async Task OversizedBody_Returns413()
    {
        await using var factory = Factory(new RecordingProvider(TaxonLookupResult.Unavailable));
        using var client = factory.CreateClient();
        var body = JsonSerializer.Serialize(new { q = "Rosa canina", padding = new string('a', 2000) });

        var response = await client.PostAsync(Url, new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"PAYLOAD_TOO_LARGE","message":"The request body is too large."}}""",
            await response.Content.ReadAsStringAsync());
    }

    // Protects against: one client using up the app-wide GBIF budget (SECURITY §13).
    [Fact]
    public async Task Lookup_IsRateLimitedPerClient()
    {
        await using var factory = Factory(new RecordingProvider(TaxonLookupResult.NotFound));
        using var client = factory.CreateClient();
        var q = new { q = $"{TestData.NewGenus()} vulgaris" };

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url, q)).StatusCode);
        }

        var rejected = await client.PostAsJsonAsync(Url, q);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(
            """{"error":{"code":"RATE_LIMITED","message":"Too many requests. Please wait a moment and try again."}}""",
            await rejected.Content.ReadAsStringAsync());
        Assert.InRange(int.Parse(Assert.Single(rejected.Headers.GetValues("Retry-After"))), 1, 60);
    }

    // Protects against: the searched name reaching server logs (privacy), including on failures.
    [Fact]
    public async Task SearchText_IsNotLogged()
    {
        var genus = TestData.NewGenus();
        await using var baseFactory = new TestApiFactory(fixture.ConnectionString);
        await using var factory = baseFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<IPlantDataProvider>(new RecordingProvider(TaxonLookupResult.Found(ExternalTaxa.Species(genus))))));
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(Url, new { q = $"{genus} vulgaris" });
        await client.PostAsync(Url, new StringContent($$"""{"q":"{{genus}}""", Encoding.UTF8, "application/json"));

        Assert.NotEmpty(baseFactory.Logs.Entries);
        Assert.DoesNotContain(baseFactory.Logs.Entries, e => e.Contains(genus, StringComparison.OrdinalIgnoreCase));
    }

    private WebApplicationFactory<Program> Factory(IPlantDataProvider provider, GenusPlantGroupMap? map = null) =>
        new TestApiFactory(fixture.ConnectionString).WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.AddSingleton(provider);
            services.AddSingleton(map ?? new GenusPlantGroupMap(null));
        }));

    private static async Task<JsonDocument> PostAsync(HttpClient client, string q)
    {
        var response = await client.PostAsJsonAsync(Url, new { q });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertUnavailableAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"EXTERNAL_LOOKUP_UNAVAILABLE","message":"External plant lookup is unavailable right now. Please try again later."}}""",
            await response.Content.ReadAsStringAsync());
    }

    private async Task<PlantGroup> NewGroupAsync()
    {
        await using var db = fixture.CreateDbContext();
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        db.PlantGroups.Add(group);
        await db.SaveChangesAsync();
        return group;
    }

    private async Task<Guid> SinglePlantIdAsync(string genus)
    {
        await using var db = fixture.CreateDbContext();
        return db.Plants.Where(p => p.ScientificName.StartsWith(genus)).Select(p => p.Id).Single();
    }

    /// <summary>Answers every lookup with <paramref name="result"/> and records the names it was asked for.</summary>
    private sealed class RecordingProvider(TaxonLookupResult result) : IPlantDataProvider
    {
        private readonly ConcurrentQueue<string> _names = new();

        public IReadOnlyCollection<string> Names => _names.ToArray();

        public Task<TaxonLookupResult> MatchAsync(string scientificName, CancellationToken cancellationToken)
        {
            _names.Enqueue(scientificName);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<ExternalCommonName>> GetCommonNamesAsync(ExternalTaxon taxon, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExternalCommonName>>([]);
    }
}
