using System.Net;
using System.Text.Json;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

[Collection(PostgresCollection.Name)]
public class CategoryEndpointTests(PostgresDatabaseFixture fixture)
{
    // Protects against: missing categories, wrong field names, or unstable ordering.
    [Fact]
    public async Task Categories_ReturnsNameOrderedList()
    {
        var b = TestData.NewCategory("B");
        var a = TestData.NewCategory("A");
        await using (var db = fixture.CreateDbContext())
        {
            db.Categories.AddRange(b, a); // inserted out of order on purpose
            await db.SaveChangesAsync();
        }

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = json.RootElement.GetProperty("data").EnumerateArray().ToList();
        var ids = items.Select(i => i.GetProperty("id").GetString()).ToList();

        Assert.True(ids.IndexOf(a.Slug) < ids.IndexOf(b.Slug));
        var item = items.Single(i => i.GetProperty("id").GetString() == a.Slug);
        Assert.Equal(["id", "name", "imageUrl"], item.EnumerateObject().Select(p => p.Name));
        Assert.Equal(a.Name, item.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("imageUrl").ValueKind);
    }

    // Protects against: other categories' groups leaking into a category page (D-017).
    [Fact]
    public async Task Category_ReturnsOnlyItsOwnGroups()
    {
        var flowers = TestData.NewCategory();
        var trees = TestData.NewCategory();
        var tulips = TestData.NewPlantGroup(flowers, "Tulips");
        var roses = TestData.NewPlantGroup(flowers, "Roses");
        var oaks = TestData.NewPlantGroup(trees, "Oaks");
        await using (var db = fixture.CreateDbContext())
        {
            db.PlantGroups.AddRange(tulips, roses, oaks);
            await db.SaveChangesAsync();
        }

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/categories/{flowers.Slug}"));
        var data = json.RootElement.GetProperty("data");

        Assert.Equal(["id", "name", "plantGroups"], data.EnumerateObject().Select(p => p.Name));
        Assert.Equal(flowers.Slug, data.GetProperty("id").GetString());
        var groups = data.GetProperty("plantGroups").EnumerateArray().ToList();
        Assert.Equal([roses.Slug, tulips.Slug], groups.Select(g => g.GetProperty("id").GetString()));
        Assert.Equal(["id", "name", "imageUrl"], groups[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal("Roses", groups[0].GetProperty("name").GetString());
    }

    // Protects against: unknown categories returning anything other than the documented 404.
    [Fact]
    public async Task UnknownCategory_Returns404()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/categories/no-such-category-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"CATEGORY_NOT_FOUND","message":"The requested category could not be found."}}""",
            await response.Content.ReadAsStringAsync());
    }
}
