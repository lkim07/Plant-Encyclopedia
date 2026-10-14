using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlantEncyclopedia.Application.Search;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

[Collection(PostgresCollection.Name)]
public class SearchEndpointTests(PostgresDatabaseFixture fixture)
{
    // Every test searches for its own unique marker word, so other tests' plants never match.
    // Markers start with "z" so tests can rely on them sorting after names starting with A–Y.
    private static string NewMarker() => "z" + Guid.NewGuid().ToString("N");

    // Protects against: a searchable name field being ignored, or the response shape drifting.
    [Fact]
    public async Task Search_FindsPlantsByEveryNameField()
    {
        var m = NewMarker();
        var group = TestData.NewPlantGroup(TestData.NewCategory(), $"Group{m}");
        var byDisplay = TestData.NewPlant(group, $"Display{m}");
        var byScientific = TestData.NewPlant(group, "Test plant");
        byScientific.ScientificName = $"Scientific{m}";
        var byCultivar = TestData.NewPlant(group, "Test plant");
        byCultivar.CultivarName = $"Cultivar{m}";
        var byCommonName = TestData.NewPlant(group, "Test plant");
        byCommonName.Names.Add(new PlantName { Name = $"장미{m}", NameType = PlantNameType.Common, LanguageCode = "ko" });
        var bySynonym = TestData.NewPlant(group, "Test plant");
        bySynonym.Names.Add(new PlantName { Name = $"Synonym{m}", NameType = PlantNameType.Synonym });
        await SaveAsync(byDisplay, byScientific, byCultivar, byCommonName, bySynonym);

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal([byDisplay.Id], await SearchIdsAsync(client, $"display{m}"));
        Assert.Equal([byScientific.Id], await SearchIdsAsync(client, $"scientific{m}"));
        Assert.Equal([byCultivar.Id], await SearchIdsAsync(client, $"cultivar{m}"));
        Assert.Equal([byCommonName.Id], await SearchIdsAsync(client, $"장미{m}"));
        Assert.Equal([bySynonym.Id], await SearchIdsAsync(client, $"synonym{m}"));
        Assert.Equal(5, (await SearchIdsAsync(client, $"group{m}")).Count);

        using var json = await SearchAsync(client, $"display{m}");
        var item = json.RootElement.GetProperty("data")[0];
        Assert.Equal(["data", "pagination"], json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "name", "plantGroup", "scientificName", "imageUrl"], item.EnumerateObject().Select(p => p.Name));
        Assert.Equal($"Display{m}", item.GetProperty("name").GetString());
        Assert.Equal($"Group{m}", item.GetProperty("plantGroup").GetString());
        Assert.Equal("Testus plantus", item.GetProperty("scientificName").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("imageUrl").ValueKind);
    }

    // Protects against: case, accents, punctuation or full-width characters preventing a match.
    [Theory]
    [InlineData("CAFE {m} MR LINCOLN")]
    [InlineData("café {m}")]
    [InlineData("Cafe{acute} {m}")] // e + combining acute accent (U+0301)
    [InlineData("ｃａｆｅ {m}")] // full-width letters
    [InlineData("{m}-mr.lincoln")]
    [InlineData("lincoln {m}")] // word order does not matter
    public async Task Search_IgnoresCaseAccentsAndPunctuation(string query)
    {
        var m = NewMarker();
        var plant = TestData.NewPlant(TestData.NewPlantGroup(TestData.NewCategory()), $"Café {m} 'Mr. Lincoln'");
        await SaveAsync(plant);

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal([plant.Id], await SearchIdsAsync(client, query.Replace("{m}", m).Replace("{acute}", ((char)0x0301).ToString())));
    }

    // Protects against: plants matching only some of the words ("peace rose" finds Rosa 'Peace' via its group).
    [Fact]
    public async Task Search_RequiresEveryWord()
    {
        var m = NewMarker();
        var roses = TestData.NewPlantGroup(TestData.NewCategory(), "Roses");
        var peace = TestData.NewPlant(roses, $"{m} 'Peace'");
        var iceberg = TestData.NewPlant(roses, $"{m} 'Iceberg'");
        await SaveAsync(peace, iceberg);

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal([peace.Id], await SearchIdsAsync(client, $"{m} peace rose"));
        Assert.Empty(await SearchIdsAsync(client, $"{m} peace iceberg"));
    }

    // Protects against: Draft plants leaking into search results.
    [Fact]
    public async Task Search_ReturnsPublishedPlantsOnly()
    {
        var m = NewMarker();
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        await SaveAsync(TestData.NewPlant(group, $"Draft {m}", PlantStatus.Draft));

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = await SearchAsync(client, m);
        Assert.Empty(json.RootElement.GetProperty("data").EnumerateArray());
        Assert.False(json.RootElement.GetProperty("pagination").GetProperty("hasNext").GetBoolean());
    }

    // Protects against: relevance being ignored in favour of alphabetical order (D-016).
    [Fact]
    public async Task Search_OrdersByRelevance()
    {
        var m = NewMarker();
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        // Alphabetical order is the reverse of the expected relevance order.
        var otherNameOnly = TestData.NewPlant(group, $"Aster {m}");
        otherNameOnly.Names.Add(new PlantName { Name = "Peace lily", NameType = PlantNameType.Common, LanguageCode = "en" });
        var containsAllWords = TestData.NewPlant(group, $"Rosa {m} 'Peace'");
        var startsWith = TestData.NewPlant(group, $"{m} Peace Garden");
        var exact = TestData.NewPlant(group, $"{m} Peace");
        await SaveAsync(otherNameOnly, containsAllWords, startsWith, exact);

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(
            [exact.Id, startsWith.Id, containsAllWords.Id, otherNameOnly.Id],
            await SearchIdsAsync(client, $"{m} peace"));
    }

    // Protects against: pages overlapping, skipping results, or a wrong hasNext.
    [Fact]
    public async Task Search_Paginates()
    {
        var m = NewMarker();
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        await SaveAsync(
            TestData.NewPlant(group, $"{m} C"),
            TestData.NewPlant(group, $"{m} A"),
            TestData.NewPlant(group, $"{m} B"));

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var page1 = await SearchAsync(client, m, "&page=1&pageSize=2");
        using var page2 = await SearchAsync(client, m, "&page=2&pageSize=2");

        Assert.Equal([$"{m} A", $"{m} B"], Names(page1));
        Assert.True(page1.RootElement.GetProperty("pagination").GetProperty("hasNext").GetBoolean());
        Assert.Equal([$"{m} C"], Names(page2));
        Assert.False(page2.RootElement.GetProperty("pagination").GetProperty("hasNext").GetBoolean());
    }

    // Protects against: SQL injection or LIKE wildcards in the search text (SECURITY §9).
    [Theory]
    [InlineData("'; DROP TABLE plants; --")]
    [InlineData("{m}' OR '1'='1")]
    [InlineData("{m}%")]
    [InlineData("{m}_")]
    [InlineData(@"{m}\")]
    public async Task Search_TreatsInjectionAttemptsAsPlainText(string query)
    {
        var m = NewMarker();
        var plant = TestData.NewPlant(TestData.NewPlantGroup(TestData.NewCategory()), m);
        await SaveAsync(plant);

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/search?q=" + Uri.EscapeDataString(query.Replace("{m}", m)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // "OR" and "1" are just words that must also match, so the injected condition matches nothing.
        if (query.Contains("OR"))
        {
            Assert.Empty(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").EnumerateArray());
        }

        // The table is intact and the plant is still found.
        Assert.Equal([plant.Id], await SearchIdsAsync(client, m));
    }

    // Protects against: invalid or abusive search requests reaching the database (SECURITY §8).
    [Theory]
    [InlineData("", """{"q":"Search text is required."}""")]
    [InlineData("q=%20%20", """{"q":"Search text is required."}""")]
    [InlineData("q=rose%00", """{"q":"Search text must not contain control characters."}""")]
    [InlineData("q=rose%0Aplant", """{"q":"Search text must not contain control characters."}""")]
    [InlineData("q=%21%3F%25", """{"q":"Search text must contain a letter or digit."}""")]
    [InlineData("q=a+b+c+d+e+f+g+h+i+j+k", """{"q":"Search text must have at most 10 words."}""")]
    [InlineData("q=rose&page=51", """{"page":"Page must be a whole number between 1 and 50."}""")]
    [InlineData("pageSize=0", """{"q":"Search text is required.","pageSize":"Page size must be a whole number between 1 and 50."}""")]
    public async Task InvalidSearch_Returns400(string query, string expectedDetails)
    {
        await AssertValidationErrorAsync("/api/search?" + query, expectedDetails);
    }

    // Protects against: unbounded search text.
    [Fact]
    public async Task TooLongSearch_Returns400()
    {
        await AssertValidationErrorAsync(
            "/api/search?q=" + new string('a', 101),
            """{"q":"Search text must be at most 100 characters."}""");
    }

    // Protects against: scripts flooding search (SECURITY §13).
    [Fact]
    public async Task Search_IsRateLimitedPerClient()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var url = "/api/search?q=" + NewMarker();

        for (var i = 0; i < 30; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
        }

        var rejected = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(
            """{"error":{"code":"RATE_LIMITED","message":"Too many requests. Please wait a moment and try again."}}""",
            await rejected.Content.ReadAsStringAsync());
        var retryAfter = Assert.Single(rejected.Headers.GetValues("Retry-After"));
        Assert.InRange(int.Parse(retryAfter), 1, 60);
    }

    // Protects against: a slow search holding the request (and a database connection) open indefinitely.
    [Fact]
    public async Task SlowSearch_TimesOutWith503()
    {
        await using var baseFactory = new TestApiFactory(fixture.ConnectionString);
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<ISearchQueries, NeverFinishingSearch>()));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/search?q=rose");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"REQUEST_TIMEOUT","message":"The request took too long. Please try again."}}""",
            await response.Content.ReadAsStringAsync());
    }

    // Protects against: users' search text ending up in server logs (privacy).
    [Fact]
    public async Task SearchText_IsNotLogged()
    {
        var secret = NewMarker();
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        await client.GetAsync("/api/search?q=" + secret);
        await client.GetAsync("/api/search?q=" + secret + "&page=abc");

        Assert.NotEmpty(factory.Logs.Entries);
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Contains(secret));
    }

    private async Task AssertValidationErrorAsync(string url, string expectedDetails)
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"VALIDATION_ERROR","message":"One or more fields are invalid.","details":""" + expectedDetails + "}}",
            await response.Content.ReadAsStringAsync());
    }

    private async Task SaveAsync(params Plant[] plants)
    {
        await using var db = fixture.CreateDbContext();
        db.Plants.AddRange(plants);
        await db.SaveChangesAsync();
    }

    private static async Task<JsonDocument> SearchAsync(HttpClient client, string query, string extra = "")
    {
        var response = await client.GetAsync("/api/search?q=" + Uri.EscapeDataString(query) + extra);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<List<Guid>> SearchIdsAsync(HttpClient client, string query)
    {
        using var json = await SearchAsync(client, query);
        return json.RootElement.GetProperty("data").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
    }

    private static List<string?> Names(JsonDocument json) =>
        json.RootElement.GetProperty("data").EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();

    private sealed class NeverFinishingSearch : ISearchQueries
    {
        public async Task<SearchPage> SearchAsync(string text, int page, int pageSize, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }
}
