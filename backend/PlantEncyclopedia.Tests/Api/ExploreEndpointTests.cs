using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// GET /api/explore (API_SPEC §20–23). The test database holds plants from other tests too, so the feed's rules are
/// checked as properties of the whole feed rather than as one exact list.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ExploreEndpointTests(PostgresDatabaseFixture fixture)
{
    // Protects against: a plant shown twice, a Published plant missing, or an unpublished plant shown.
    [Fact]
    public async Task Feed_ShowsEveryPublishedPlantOnce()
    {
        var (_, plants, draft) = await CreateCategoryAsync();
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var feed = await ReadWholeFeedAsync(client, pageSize: 50);

        Assert.Equal(feed.Count, feed.Select(c => c.Id).Distinct().Count());
        Assert.All(plants, p => Assert.Contains(feed, c => c.Id == p.Id));
        Assert.DoesNotContain(feed, c => c.Id == draft.Id);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(await db.Plants.CountAsync(p => p.Status == PlantStatus.Published && p.PlantGroupId != null), feed.Count);
    }

    // Protects against: a feed like "Rose, Rose, Rose, Tulip" (UX §13). Categories take turns, and so do the
    // groups inside a category: the n-th card of one never comes after the (n+1)-th card of another.
    [Fact]
    public async Task Feed_TakesTurnsAcrossCategoriesAndGroups()
    {
        await CreateCategoryAsync();
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var groups = await GroupsByPlantAsync();

        var feed = await ReadWholeFeedAsync(client, pageSize: 50);

        AssertTakesTurns(feed.Select(c => groups[c.Id].Category).ToList());
        foreach (var category in feed.GroupBy(c => groups[c.Id].Category))
        {
            AssertTakesTurns(category.Select(c => groups[c.Id].Group).ToList());
        }
    }

    // Protects against: plants without images hiding the ones with images at the top of their group.
    [Fact]
    public async Task Feed_ShowsAGroupsPlantsWithImagesFirst()
    {
        var (_, plants, _) = await CreateCategoryAsync();
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var feed = await ReadWholeFeedAsync(client, pageSize: 50);

        var roses = feed.Where(c => plants.Take(3).Any(p => p.Id == c.Id)).ToList();
        Assert.Equal(["/media/z-medium.webp", null, null], roses.Select(c => c.ImageUrl));
        Assert.Equal("Zinnia", roses[0].Name.Split(' ')[0]); // last by name, first because it has an image
    }

    // Protects against: pages repeating or skipping plants (infinite scroll relies on a stable order).
    [Fact]
    public async Task Feed_PagesAreStable()
    {
        await CreateCategoryAsync();
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var small = await ReadWholeFeedAsync(client, pageSize: 7);
        var large = await ReadWholeFeedAsync(client, pageSize: 50);

        Assert.Equal(large.Select(c => c.Id), small.Select(c => c.Id));
    }

    [Fact]
    public async Task Feed_ReturnsTheCardShapeAndPagination()
    {
        await CreateCategoryAsync();
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/explore?pageSize=1"));

        var card = Assert.Single(json.RootElement.GetProperty("data").EnumerateArray().ToList());
        Assert.Equal(["id", "name", "plantGroup", "imageUrl"], card.EnumerateObject().Select(p => p.Name));
        Assert.Equal("""{"page":1,"pageSize":1,"hasNext":true}""", json.RootElement.GetProperty("pagination").GetRawText());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=51")]
    [InlineData("page=abc")]
    public async Task Feed_RejectsInvalidPaging(string query)
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/explore?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", await response.Content.ReadAsStringAsync());
    }

    // The n-th appearance of any value comes before the (n+1)-th appearance of every other value that has one.
    private static void AssertTakesTurns(IReadOnlyList<string> sequence)
    {
        var seen = new Dictionary<string, int>();
        var turns = sequence.Select(v => seen[v] = seen.GetValueOrDefault(v) + 1).ToList();
        for (var i = 1; i < turns.Count; i++)
        {
            Assert.True(turns[i] >= turns[i - 1], $"Turn {turns[i]} of '{sequence[i]}' comes after turn {turns[i - 1]} at position {i}.");
        }
    }

    private static async Task<List<Card>> ReadWholeFeedAsync(HttpClient client, int pageSize)
    {
        var cards = new List<Card>();
        for (var page = 1; ; page++)
        {
            using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/explore?page={page}&pageSize={pageSize}"));
            cards.AddRange(json.RootElement.GetProperty("data").EnumerateArray().Select(c => new Card(
                c.GetProperty("id").GetGuid(),
                c.GetProperty("name").GetString()!,
                c.GetProperty("imageUrl").GetString())));
            if (!json.RootElement.GetProperty("pagination").GetProperty("hasNext").GetBoolean()) return cards;
        }
    }

    private async Task<Dictionary<Guid, (string Category, string Group)>> GroupsByPlantAsync()
    {
        await using var db = fixture.CreateDbContext();
        var rows = await db.Plants
            .Where(p => p.PlantGroup != null)
            .Select(p => new { p.Id, Category = p.PlantGroup!.Category!.Slug, Group = p.PlantGroup.Slug })
            .ToListAsync();
        return rows.ToDictionary(r => r.Id, r => (r.Category, r.Group));
    }

    /// <summary>
    /// A category with two groups: three "roses" (only the last by name has an image) and one "tulip", plus a
    /// Draft plant that must never appear.
    /// </summary>
    private async Task<(Category Category, List<Plant> Plants, Plant Draft)> CreateCategoryAsync()
    {
        await using var db = fixture.CreateDbContext();
        var category = TestData.NewCategory();
        var roses = TestData.NewPlantGroup(category, "Roses");
        var tulips = TestData.NewPlantGroup(category, "Tulips");
        var suffix = Guid.NewGuid().ToString("N");

        var zinnia = TestData.NewPlant(roses, $"Zinnia {suffix}");
        zinnia.Images.Add(new PlantImage
        {
            Url = "/media/z-large.webp", MediumUrl = "/media/z-medium.webp", Source = "test", RetrievedAt = DateTimeOffset.UtcNow
        });
        var plants = new List<Plant>
        {
            TestData.NewPlant(roses, $"Alba {suffix}"),
            TestData.NewPlant(roses, $"Bella {suffix}"),
            zinnia,
            TestData.NewPlant(tulips, $"Tulip {suffix}")
        };
        var draft = TestData.NewPlant(tulips, $"Draft {suffix}", PlantStatus.Draft);

        db.Plants.AddRange(plants);
        db.Plants.Add(draft);
        await db.SaveChangesAsync();

        // Feed order within the roses: Zinnia (image), then Alba, Bella.
        plants = [zinnia, plants[0], plants[1], plants[3]];
        return (category, plants, draft);
    }

    private sealed record Card(Guid Id, string Name, string? ImageUrl);
}
