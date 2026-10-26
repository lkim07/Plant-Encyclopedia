using System.Net;
using System.Text.Json;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// GET /api/identification/related (the More page, API_SPEC §44): plants related to the identification's candidates,
/// closest first, each once, without the candidates. Every test makes its own categories, groups and taxa, so the
/// expected lists are exact even though the database holds plants from other tests.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RelatedPlantsEndpointTests(PostgresDatabaseFixture fixture)
{
    // Protects against: More showing unrelated plants first, repeating the candidate, or showing drafts (UX §37).
    [Fact]
    public async Task Related_AreClosestFirst_GroupThenGenusFamilyAndCategory()
    {
        await using var db = fixture.CreateDbContext();
        var (genus, family) = (TestData.NewGenus(), $"Testfamilia{Guid.NewGuid():N}");
        var category = TestData.NewCategory();
        var group = TestData.NewPlantGroup(category, "Roses");
        var sameCategoryGroup = TestData.NewPlantGroup(category, "Tulips");

        var seed = Plant(group, "Seed", genus, family);
        var groupWithImage = Plant(group, "Zinnia", image: true);
        var groupWithoutImage = Plant(group, "Aster");
        var sameGenus = Plant(TestData.NewPlantGroup(TestData.NewCategory()), "Genus mate", genus, family);
        var sameFamily = Plant(null, "Family mate", TestData.NewGenus(), family); // imported without a group (D-072)
        var sameCategory = Plant(sameCategoryGroup, "Category mate");
        var unrelated = Plant(TestData.NewPlantGroup(TestData.NewCategory()), "Unrelated", TestData.NewGenus(), "Otherfamilia");
        var draft = Plant(group, "Draft", status: PlantStatus.Draft);
        db.Plants.AddRange(seed, groupWithImage, groupWithoutImage, sameGenus, sameFamily, sameCategory, unrelated, draft);
        await db.SaveChangesAsync();

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var related = await NamesAsync(client, $"plantId={seed.Id}&pageSize=50");

        Assert.Equal(["Zinnia", "Aster", "Genus mate", "Family mate", "Category mate"], related);
    }

    // Protects against: the #2 candidate's relatives crowding out #1's.
    [Fact]
    public async Task Related_PutTheFirstCandidatesRelativesFirst()
    {
        await using var db = fixture.CreateDbContext();
        var (first, second) = (TestData.NewPlantGroup(TestData.NewCategory()), TestData.NewPlantGroup(TestData.NewCategory()));
        var (seed1, seed2) = (Plant(first, "Seed 1"), Plant(second, "Seed 2"));
        db.Plants.AddRange(seed1, seed2, Plant(second, "B relative"), Plant(first, "Z relative"));
        await db.SaveChangesAsync();

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var related = await NamesAsync(client, $"plantId={seed1.Id}&plantId={seed2.Id}&pageSize=50");

        Assert.Equal(["Z relative", "B relative"], related); // seeds are candidates, so they are left out
    }

    // Protects against: infinite scroll repeating or skipping plants on the More page.
    [Fact]
    public async Task Related_PagesAreStable()
    {
        await using var db = fixture.CreateDbContext();
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        var seed = Plant(group, "Seed");
        db.Plants.Add(seed);
        db.Plants.AddRange(Enumerable.Range(1, 5).Select(i => Plant(group, $"Relative {i}")));
        await db.SaveChangesAsync();

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var paged = new List<string>();
        for (var page = 1; page <= 3; page++) paged.AddRange(await NamesAsync(client, $"plantId={seed.Id}&page={page}&pageSize=2"));

        Assert.Equal(await NamesAsync(client, $"plantId={seed.Id}&pageSize=50"), paged);
        Assert.Equal(5, paged.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("plantId=not-a-uuid")]
    [InlineData("plantId=00000000-0000-0000-0000-000000000000")]
    public async Task Related_NeedValidCandidateIds(string query)
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/identification/related?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"plantId\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Related_AcceptAtMostThreeCandidates()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var four = string.Join("&", Enumerable.Range(0, 4).Select(_ => $"plantId={Guid.NewGuid()}"));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/identification/related?{four}")).StatusCode);
    }

    private static Plant Plant(PlantGroup? group, string name, string? genus = null, string? family = null,
        bool image = false, PlantStatus status = PlantStatus.Published)
    {
        var plant = group is null
            ? new Plant { DisplayName = name, ScientificName = TestData.NewScientificName(), Status = status }
            : TestData.NewPlant(group, name, status);
        plant.Taxonomy = new Taxonomy { Kingdom = "Plantae", Genus = genus ?? TestData.NewGenus(), Family = family ?? $"Testfamilia{Guid.NewGuid():N}" };
        if (image)
        {
            plant.Images.Add(new PlantImage { Url = "/media/x.webp", MediumUrl = "/media/x-medium.webp", Source = "test", RetrievedAt = DateTimeOffset.UtcNow });
        }

        return plant;
    }

    private static async Task<List<string>> NamesAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/identification/related?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToList();
    }
}
