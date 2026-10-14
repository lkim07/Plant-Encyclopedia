using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlantEncyclopedia.Application.Search;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

[Collection(PostgresCollection.Name)]
public class SearchSuggestionEndpointTests(PostgresDatabaseFixture fixture)
{
    // Every test types its own unique marker, so other tests' names never match.
    private static string NewMarker() => "z" + Guid.NewGuid().ToString("N");

    // Protects against: a name source being ignored, wrong type labels, or the response shape drifting.
    [Fact]
    public async Task Suggestions_CoverEveryNameType()
    {
        var m = NewMarker();
        var group = TestData.NewPlantGroup(TestData.NewCategory(), $"{m}group");
        var byDisplay = TestData.NewPlant(group, $"{m}display");
        var byCultivar = TestData.NewPlant(group, "Test plant");
        byCultivar.CultivarName = $"{m}cultivar";
        var byScientific = TestData.NewPlant(group, "Test plant");
        byScientific.ScientificName = $"{m}scientific";
        var byCommonName = TestData.NewPlant(group, "Test plant");
        byCommonName.Names.Add(new PlantName { Name = $"{m}common", NameType = PlantNameType.Common, LanguageCode = "ko" });
        var bySynonym = TestData.NewPlant(group, "Test plant");
        bySynonym.Names.Add(new PlantName { Name = $"{m}synonym", NameType = PlantNameType.Synonym });
        await SaveAsync(byDisplay, byCultivar, byScientific, byCommonName, bySynonym);

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = await SuggestAsync(client, m);
        var items = json.RootElement.GetProperty("data").EnumerateArray().ToList();

        Assert.Equal(["text", "type"], items[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal(
            new HashSet<(string?, string?)>
            {
                ($"{m}display", "plant"),
                ($"{m}cultivar", "cultivar"),
                ($"{m}scientific", "scientificName"),
                ($"{m}common", "commonName"),
                ($"{m}synonym", "synonym"),
                ($"{m}group", "plantGroup")
            },
            items.Select(TextAndType).ToHashSet());
    }

    // Protects against: mid-word matches (suggestions follow word starts; full search covers the rest).
    [Fact]
    public async Task Suggestions_MatchWordStartsOnly()
    {
        var m = NewMarker();
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        var wordStart = TestData.NewPlant(group, $"Rosa {m}alpha");
        await SaveAsync(wordStart, TestData.NewPlant(group, $"Rosa x{m}beta"));

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal([$"Rosa {m}alpha"], await TextsAsync(client, m));
        Assert.Equal([$"Rosa {m}alpha"], await TextsAsync(client, $"ROSA {m}AL"));
    }

    // Protects against: case, accents or punctuation preventing suggestions.
    [Fact]
    public async Task Suggestions_IgnoreCaseAccentsAndPunctuation()
    {
        var m = NewMarker();
        await SaveAsync(TestData.NewPlant(TestData.NewPlantGroup(TestData.NewCategory()), $"Café 'Mr. {m}'"));

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal([$"Café 'Mr. {m}'"], await TextsAsync(client, $"CAFE MR.{m}"));
    }

    // Protects against: Draft plants, or groups without Published plants, being suggested.
    [Fact]
    public async Task Suggestions_ExcludeUnpublished()
    {
        var m = NewMarker();
        var draftOnlyGroup = TestData.NewPlantGroup(TestData.NewCategory(), $"{m}group");
        await SaveAsync(TestData.NewPlant(draftOnlyGroup, $"{m}draft", PlantStatus.Draft));

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Empty(await TextsAsync(client, m));
    }

    // Protects against: weaker matches crowding out names that start with the typed text.
    [Fact]
    public async Task Suggestions_OrderStartsWithThenShorterNames()
    {
        var m = NewMarker();
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        await SaveAsync(
            TestData.NewPlant(group, $"Aster {m}"),
            TestData.NewPlant(group, $"{m} long name"),
            TestData.NewPlant(group, $"{m} b"));

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal([$"{m} b", $"{m} long name", $"Aster {m}"], await TextsAsync(client, m));
    }

    // Protects against: the same text listed twice.
    [Fact]
    public async Task Suggestions_MergeIdenticalTexts()
    {
        var m = NewMarker();
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        var first = TestData.NewPlant(group, "Test plant");
        first.Names.Add(new PlantName { Name = $"{m} shared", NameType = PlantNameType.Common, LanguageCode = "en" });
        var second = TestData.NewPlant(group, "Test plant");
        second.Names.Add(new PlantName { Name = $"{m} Shared", NameType = PlantNameType.Common, LanguageCode = "fr" });
        var self = TestData.NewPlant(group, $"{m} self");
        self.Names.Add(new PlantName { Name = $"{m.ToUpperInvariant()} SELF", NameType = PlantNameType.Common, LanguageCode = "en" });
        await SaveAsync(first, second, self);

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = await SuggestAsync(client, m);
        var items = json.RootElement.GetProperty("data").EnumerateArray().Select(TextAndType).ToList();

        Assert.Equal(2, items.Count);
        // Two plants share the name (different capitalization): one suggestion.
        Assert.Contains(items, i => i.Type == "commonName"
            && string.Equals(i.Text, $"{m} shared", StringComparison.OrdinalIgnoreCase));
        // A display name and a common name fold to the same text: the display name wins.
        Assert.Contains(items, i => i == ($"{m} self", "plant"));
    }

    // Protects against: unbounded suggestion lists.
    [Fact]
    public async Task Suggestions_AreCappedAtEight()
    {
        var m = NewMarker();
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        await SaveAsync(Enumerable.Range(0, 10).Select(i => TestData.NewPlant(group, $"{m} {i}")).ToArray());

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(8, (await TextsAsync(client, m)).Count);
    }

    // Protects against: one-character text matching most of the database on every keystroke.
    [Theory]
    [InlineData("a")]
    [InlineData("a.")]
    [InlineData("É")] // folds to "e"
    public async Task ShortText_ReturnsNoSuggestions(string query)
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Empty(await TextsAsync(client, query));
    }

    // Protects against: suggestions skipping the search-text validation (SECURITY §8).
    [Theory]
    [InlineData("", "Search text is required.")]
    [InlineData("?q=rose%00", "Search text must not contain control characters.")]
    [InlineData("?q=%21%3F", "Search text must contain a letter or digit.")]
    [InlineData("?q=a+b+c+d+e+f+g+h+i+j+k", "Search text must have at most 10 words.")]
    public async Task InvalidText_Returns400(string query, string message)
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/search/suggestions" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"VALIDATION_ERROR","message":"One or more fields are invalid.","details":{"q":""" + "\"" + message + "\"}}}",
            await response.Content.ReadAsStringAsync());
    }

    // Protects against: typing using up the search allowance, or suggestions being unlimited.
    [Fact]
    public async Task Suggestions_HaveTheirOwnRateLimit()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var text = NewMarker();

        // Exhaust the search limit; suggestions are still allowed.
        for (var i = 0; i < 31; i++)
        {
            await client.GetAsync("/api/search?q=" + text);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/search?q=" + text)).StatusCode);

        for (var i = 0; i < 120; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/search/suggestions?q=" + text)).StatusCode);
        }

        var rejected = await client.GetAsync("/api/search/suggestions?q=" + text);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(
            """{"error":{"code":"RATE_LIMITED","message":"Too many requests. Please wait a moment and try again."}}""",
            await rejected.Content.ReadAsStringAsync());
        Assert.True(rejected.Headers.Contains("Retry-After"));
    }

    // Protects against: a slow suggestion query holding requests open while the user keeps typing.
    [Fact]
    public async Task SlowSuggestions_TimeOutWith503()
    {
        await using var baseFactory = new TestApiFactory(fixture.ConnectionString);
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<ISearchQueries, NeverFinishingSearch>()));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/search/suggestions?q=rose");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"REQUEST_TIMEOUT","message":"The request took too long. Please try again."}}""",
            await response.Content.ReadAsStringAsync());
    }

    // Protects against: typed text ending up in server logs (privacy).
    [Fact]
    public async Task TypedText_IsNotLogged()
    {
        var secret = NewMarker();
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        await client.GetAsync("/api/search/suggestions?q=" + secret);

        Assert.NotEmpty(factory.Logs.Entries);
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Contains(secret));
    }

    private async Task SaveAsync(params Plant[] plants)
    {
        await using var db = fixture.CreateDbContext();
        db.Plants.AddRange(plants);
        await db.SaveChangesAsync();
    }

    private static async Task<JsonDocument> SuggestAsync(HttpClient client, string text)
    {
        var response = await client.GetAsync("/api/search/suggestions?q=" + Uri.EscapeDataString(text));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<List<string?>> TextsAsync(HttpClient client, string text)
    {
        using var json = await SuggestAsync(client, text);
        return json.RootElement.GetProperty("data").EnumerateArray().Select(i => i.GetProperty("text").GetString()).ToList();
    }

    private static (string? Text, string? Type) TextAndType(JsonElement item) => (
        item.GetProperty("text").GetString(),
        item.GetProperty("type").GetString());

    private sealed class NeverFinishingSearch : ISearchQueries
    {
        public async Task<SearchPage> SearchAsync(string text, int page, int pageSize, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }

        public async Task<IReadOnlyList<SearchSuggestion>> SuggestAsync(string text, int limit, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }
}
