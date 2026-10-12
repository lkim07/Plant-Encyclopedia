using System.Net;
using System.Text.Json;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

[Collection(PostgresCollection.Name)]
public class PlantGroupEndpointTests(PostgresDatabaseFixture fixture)
{
    // Protects against: drafts or other groups' plants appearing, or the response shape drifting.
    [Fact]
    public async Task PlantGroup_ReturnsPublishedPlantsOnly()
    {
        var category = TestData.NewCategory();
        var group = TestData.NewPlantGroup(category, "Roses");
        var otherGroup = TestData.NewPlantGroup(category, "Tulips");
        await SaveAsync(
            TestData.NewPlant(group, "Test Plant B"),
            TestData.NewPlant(group, "Test Plant A"),
            TestData.NewPlant(group, "Test Plant C (draft)", PlantStatus.Draft),
            TestData.NewPlant(otherGroup, "Test Plant in another group"));

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/plant-groups/{group.Slug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var data = root.GetProperty("data");

        Assert.Equal(["data", "pagination"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "name", "category", "plants"], data.EnumerateObject().Select(p => p.Name));
        Assert.Equal(group.Slug, data.GetProperty("id").GetString());
        Assert.Equal(category.Slug, data.GetProperty("category").GetProperty("id").GetString());
        Assert.Equal(category.Name, data.GetProperty("category").GetProperty("name").GetString());

        var plants = data.GetProperty("plants").EnumerateArray().ToList();
        Assert.Equal(["Test Plant A", "Test Plant B"], plants.Select(p => p.GetProperty("name").GetString()));
        Assert.Equal(["id", "name", "imageUrl"], plants[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal(JsonValueKind.Null, plants[0].GetProperty("imageUrl").ValueKind);

        // Defaults: page 1, 20 per page.
        var pagination = root.GetProperty("pagination");
        Assert.Equal(1, pagination.GetProperty("page").GetInt32());
        Assert.Equal(20, pagination.GetProperty("pageSize").GetInt32());
        Assert.False(pagination.GetProperty("hasNext").GetBoolean());
    }

    // Protects against: pages overlapping, skipping plants, or a wrong hasNext.
    [Fact]
    public async Task PlantGroup_Paginates()
    {
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        await SaveAsync(
            TestData.NewPlant(group, "Test Plant C"),
            TestData.NewPlant(group, "Test Plant A"),
            TestData.NewPlant(group, "Test Plant B"));

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var page1 = JsonDocument.Parse(await client.GetStringAsync($"/api/plant-groups/{group.Slug}?page=1&pageSize=2"));
        using var page2 = JsonDocument.Parse(await client.GetStringAsync($"/api/plant-groups/{group.Slug}?page=2&pageSize=2"));

        Assert.Equal(["Test Plant A", "Test Plant B"], PlantNames(page1));
        Assert.True(page1.RootElement.GetProperty("pagination").GetProperty("hasNext").GetBoolean());

        Assert.Equal(["Test Plant C"], PlantNames(page2));
        var pagination2 = page2.RootElement.GetProperty("pagination");
        Assert.Equal(2, pagination2.GetProperty("page").GetInt32());
        Assert.Equal(2, pagination2.GetProperty("pageSize").GetInt32());
        Assert.False(pagination2.GetProperty("hasNext").GetBoolean());
    }

    // Protects against: unlimited or malformed page requests (API_SPEC §10).
    [Theory]
    [InlineData("pageSize=0", """{"pageSize":"Page size must be a whole number between 1 and 50."}""")]
    [InlineData("pageSize=51", """{"pageSize":"Page size must be a whole number between 1 and 50."}""")]
    [InlineData("page=abc", """{"page":"Page must be a whole number between 1 and 10000."}""")]
    [InlineData("page=0&pageSize=x", """{"page":"Page must be a whole number between 1 and 10000.","pageSize":"Page size must be a whole number between 1 and 50."}""")]
    public async Task InvalidPagination_Returns400(string query, string expectedDetails)
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/plant-groups/any-group?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"VALIDATION_ERROR","message":"One or more fields are invalid.","details":""" + expectedDetails + "}}",
            await response.Content.ReadAsStringAsync());
    }

    // Protects against: unknown groups returning anything other than the documented 404.
    [Fact]
    public async Task UnknownPlantGroup_Returns404()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/plant-groups/no-such-group-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"PLANT_GROUP_NOT_FOUND","message":"The requested plant group could not be found."}}""",
            await response.Content.ReadAsStringAsync());
    }

    // Protects against: a group without Published plants being treated as missing.
    [Fact]
    public async Task EmptyPlantGroup_ReturnsEmptyList()
    {
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        await SaveAsync(TestData.NewPlant(group, "Test Plant (draft only)", PlantStatus.Draft));

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/plant-groups/{group.Slug}"));

        Assert.Empty(PlantNames(json));
        Assert.False(json.RootElement.GetProperty("pagination").GetProperty("hasNext").GetBoolean());
    }

    private async Task SaveAsync(params Plant[] plants)
    {
        await using var db = fixture.CreateDbContext();
        db.Plants.AddRange(plants);
        await db.SaveChangesAsync();
    }

    private static List<string?> PlantNames(JsonDocument json) =>
        json.RootElement.GetProperty("data").GetProperty("plants").EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();
}
