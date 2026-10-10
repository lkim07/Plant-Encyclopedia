using System.Net;
using System.Text.Json;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

[Collection(PostgresCollection.Name)]
public class PlantDetailEndpointTests(PostgresDatabaseFixture fixture)
{
    private static readonly string[] ExpectedFields =
        ["id", "name", "plantGroup", "scientificName", "description", "heroImage", "quickCare"];

    // Protects against: missing fields, or database columns leaking into the public response.
    [Fact]
    public async Task Published_ReturnsPlantDetail()
    {
        var plant = await CreatePublishedPlantAsync(care: null);
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/plants/{plant.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");

        Assert.Equal(ExpectedFields, data.EnumerateObject().Select(p => p.Name));
        Assert.Equal(plant.Id, data.GetProperty("id").GetGuid());
        Assert.Equal(plant.DisplayName, data.GetProperty("name").GetString());
        Assert.Equal("Test Group", data.GetProperty("plantGroup").GetString());
        Assert.Equal("Testus plantus", data.GetProperty("scientificName").GetString());
        Assert.Equal("Test description", data.GetProperty("description").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("heroImage").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("quickCare").ValueKind);
    }

    // Protects against: unknown IDs returning anything other than the documented 404 error.
    [Fact]
    public async Task UnknownId_Returns404()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/plants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"PLANT_NOT_FOUND","message":"The requested plant could not be found."}}""",
            await response.Content.ReadAsStringAsync());
    }

    // Protects against: unpublished plants being visible to the public.
    [Fact]
    public async Task DraftPlant_Returns404()
    {
        Guid draftId;
        await using (var db = fixture.CreateDbContext())
        {
            var draft = TestData.NewPlant(); // Draft by default, no group
            db.Plants.Add(draft);
            await db.SaveChangesAsync();
            draftId = draft.Id;
        }

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/plants/{draftId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("PLANT_NOT_FOUND", await response.Content.ReadAsStringAsync());
    }

    // Protects against: a malformed ID being reported as "not found" instead of a client error.
    [Fact]
    public async Task InvalidId_Returns400()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/plants/abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"VALIDATION_ERROR","message":"One or more fields are invalid.","details":{"plantId":"Plant ID must be a valid UUID."}}}""",
            await response.Content.ReadAsStringAsync());
    }

    // Protects against: verified care not reaching the Quick Care cards, or temperature losing its numbers.
    [Fact]
    public async Task VerifiedCare_IsReturned()
    {
        var plant = await CreatePublishedPlantAsync(new PlantCare
        {
            LightSummary = "Test light",
            LightDetails = "Test light details",
            TemperatureMinC = 15,
            TemperatureMaxC = 25,
            SoilSummary = "Test soil",
            VerificationStatus = CareVerificationStatus.Verified,
            VerifiedAt = DateTimeOffset.UtcNow
        });

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/plants/{plant.Id}"));
        var care = json.RootElement.GetProperty("data").GetProperty("quickCare");

        Assert.Equal("Test light", care.GetProperty("light").GetProperty("value").GetString());
        Assert.Equal("Test light details", care.GetProperty("light").GetProperty("description").GetString());
        Assert.Equal(JsonValueKind.Null, care.GetProperty("water").ValueKind); // no water value
        Assert.Equal(15m, care.GetProperty("temperature").GetProperty("minC").GetDecimal());
        Assert.Equal(25m, care.GetProperty("temperature").GetProperty("maxC").GetDecimal());
        Assert.Equal("Test soil", care.GetProperty("soil").GetProperty("value").GetString());
        Assert.Equal(JsonValueKind.Null, care.GetProperty("soil").GetProperty("description").ValueKind);
    }

    // Protects against: unverified care being presented as if it were verified.
    [Fact]
    public async Task UnverifiedCare_IsHidden()
    {
        var plant = await CreatePublishedPlantAsync(new PlantCare
        {
            LightSummary = "Test light",
            VerificationStatus = CareVerificationStatus.Unverified
        });

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/plants/{plant.Id}"));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").GetProperty("quickCare").ValueKind);
    }

    // Protects against: database errors reaching the client from a real endpoint
    // (end-to-end check of GlobalExceptionHandler).
    [Fact]
    public async Task DatabaseFailure_Returns500SafeError()
    {
        var passwordMarker = $"pw-{Guid.NewGuid():N}";
        var unreachable =
            $"Host=127.0.0.1;Port=1;Database={PostgresDatabaseFixture.TestDatabaseName};" +
            $"Username=test;Password={passwordMarker};Timeout=2";

        await using var factory = new TestApiFactory(unreachable);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/plants/{Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"INTERNAL_SERVER_ERROR","message":"Something went wrong. Please try again."}}""",
            body);
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Contains(passwordMarker));
        Assert.Contains(factory.Logs.Entries, entry => entry.Contains("Unhandled exception while processing GET /api/plants/"));
    }

    private async Task<Plant> CreatePublishedPlantAsync(PlantCare? care)
    {
        await using var db = fixture.CreateDbContext();
        var suffix = Guid.NewGuid().ToString("N");

        var group = new PlantGroup
        {
            Name = "Test Group",
            Slug = $"test-group-{suffix}",
            Category = new Category { Name = $"Test Category {suffix}", Slug = $"test-category-{suffix}" }
        };

        var plant = TestData.NewPlant();
        plant.PlantGroup = group;
        plant.Status = PlantStatus.Published;
        plant.Description = "Test description";
        plant.Care = care;

        db.Plants.Add(plant);
        await db.SaveChangesAsync();
        return plant;
    }
}
