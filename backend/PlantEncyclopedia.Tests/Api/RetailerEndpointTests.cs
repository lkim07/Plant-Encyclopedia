using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantEncyclopedia.Application.Retail;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Domain.Retail;
using PlantEncyclopedia.Infrastructure.Retail;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// Where to Buy (API_SPEC §45–51) against PostgreSQL: distance and relevance order, the radius, shop kinds by plant
/// category, honest availability, attribution, validation, and the snapshot refresh. Each test puts its shops around
/// its own random point, far from other tests' shops.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RetailerEndpointTests(PostgresDatabaseFixture fixture)
{
    // Protects against: far shops before near ones, shops outside the radius, or stock and ratings being implied (D-033).
    [Fact]
    public async Task Retailers_AreNearestFirst_WithinTheRadius_AndNeverClaimStock()
    {
        var (lat, lon) = RandomPoint();
        var flower = await CreatePlantAsync("flowers");
        await AddShopsAsync(
            Shop("Far florist", RetailerKind.FlowerShop, lat + 0.05, lon),      // about 5.6 km
            Shop("Near nursery", RetailerKind.Nursery, lat + 0.01, lon),        // about 1.1 km
            Shop("Too far centre", RetailerKind.GardenCentre, lat + 0.3, lon), // about 33 km
            // Inside the rough search box but about 31 km away diagonally: only the exact distance check drops it.
            Shop("Corner centre", RetailerKind.GardenCentre, lat + 0.2, lon + 0.2 / Math.Cos(lat * Math.PI / 180)));

        using var json = await GetAsync($"/api/plants/{flower.Id}/retailers?{At(lat, lon)}");

        var data = json.RootElement.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(["Near nursery", "Far florist"], data.Select(r => r.GetProperty("name").GetString()));
        Assert.Equal(1.1, data[0].GetProperty("location").GetProperty("distanceKm").GetDouble());
        Assert.Equal("nursery", data[0].GetProperty("type").GetString());
        Assert.All(data, r =>
        {
            Assert.Equal("unknown", r.GetProperty("availability").GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, r.GetProperty("rating").ValueKind);
        });
        var attribution = json.RootElement.GetProperty("attribution");
        Assert.Equal("© OpenStreetMap contributors", attribution.GetProperty("notice").GetString());
        Assert.Equal("ODbL 1.0", attribution.GetProperty("license").GetString());
    }

    // Protects against: florists offered for a tree, or relevance not putting florists first for flowers.
    [Fact]
    public async Task Retailers_MatchTheShopKindToThePlant()
    {
        var (lat, lon) = RandomPoint();
        var (tree, flower) = (await CreatePlantAsync("trees"), await CreatePlantAsync("flowers"));
        await AddShopsAsync(
            Shop("Nursery", RetailerKind.Nursery, lat + 0.01, lon),
            Shop("Florist", RetailerKind.FlowerShop, lat + 0.02, lon));

        using var forTree = await GetAsync($"/api/plants/{tree.Id}/retailers?{At(lat, lon)}");
        using var byRelevance = await GetAsync($"/api/plants/{flower.Id}/retailers?{At(lat, lon)}&sort=relevance");

        Assert.Equal(["Nursery"], Names(forTree));
        Assert.Equal(["Florist", "Nursery"], Names(byRelevance));
    }

    [Theory]
    [InlineData("latitude=91&longitude=0", "latitude")]
    [InlineData("latitude=49", "longitude")]
    [InlineData("latitude=49&longitude=-123&radius=500", "radius")]
    [InlineData("latitude=49&longitude=-123&sort=rating", "sort")]
    public async Task Retailers_ValidateTheQuery(string query, string field)
    {
        var plant = await CreatePlantAsync("flowers");
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/plants/{plant.Id}/retailers?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Retailers_ForAnUnknownOrUnpublishedPlant_AreNotFound()
    {
        var draft = await CreatePlantAsync("flowers", PlantStatus.Draft);
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/plants/{draft.Id}/retailers?latitude=49&longitude=-123")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/plants/{Guid.NewGuid()}/retailers?latitude=49&longitude=-123")).StatusCode);
    }

    // Protects against: no way to pick a location by hand when location access is denied (D-035).
    [Fact]
    public async Task Places_ListTownsWithTheAveragePositionOfTheirShops()
    {
        var (lat, lon) = RandomPoint();
        var town = $"Testville {Guid.NewGuid():N}";
        await AddShopsAsync(
            Shop("A", RetailerKind.Nursery, lat, lon, city: town),
            Shop("B", RetailerKind.Nursery, lat + 0.02, lon, city: town));
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/retailers/places"));

        var place = json.RootElement.GetProperty("data").EnumerateArray().Single(p => p.GetProperty("name").GetString() == town);
        Assert.Equal(Math.Round(lat + 0.01, 4), place.GetProperty("latitude").GetDouble(), 4);
    }

    // Protects against: a refresh duplicating shops, keeping closed ones, or touching another source's shops.
    [Fact]
    public async Task Refresh_UpdatesAddsAndRemoves_OnlyForItsSource()
    {
        var source = $"t-{Guid.NewGuid():N}"[..24];
        var other = $"t-{Guid.NewGuid():N}"[..24];
        await using (var db = fixture.CreateDbContext())
        {
            db.Retailers.AddRange(
                Shop("Kept", RetailerKind.Nursery, 49, -123, source: source, sourceId: "node/1"),
                Shop("Closed", RetailerKind.Nursery, 49, -123, source: source, sourceId: "node/2"),
                Shop("Other source", RetailerKind.Nursery, 49, -123, source: other, sourceId: "node/2"));
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateDbContext())
        {
            var summary = await new RetailerStore(db).ReplaceAsync(new RetailerSnapshot(source,
            [
                new RetailerRecord("node/1", "Kept, renamed", RetailerKind.GardenCentre, null, null, "BC", null, null, 49, -123),
                new RetailerRecord("way/3", "New", RetailerKind.FlowerShop, null, null, "BC", null, null, 49, -123)
            ], DateTimeOffset.UtcNow), CancellationToken.None);
            Assert.Equal(new RetailerImportSummary(Added: 1, Updated: 1, Removed: 1), summary);
        }

        await using var check = fixture.CreateDbContext();
        Assert.Equal(["Kept, renamed", "New"], await check.Retailers.Where(r => r.Source == source).OrderBy(r => r.Name).Select(r => r.Name).ToListAsync());
        Assert.True(await check.Retailers.AnyAsync(r => r.Source == other));
    }

    // Somewhere in the open Pacific, different for every test, so no other test's shops are within 100 km.
    private static (double Latitude, double Longitude) RandomPoint() =>
        (Math.Round(-50 + Random.Shared.NextDouble() * 60, 4), Math.Round(-170 + Random.Shared.NextDouble() * 40, 4));

    private static string At(double lat, double lon) =>
        string.Create(CultureInfo.InvariantCulture, $"latitude={lat}&longitude={lon}");

    private static Retailer Shop(string name, RetailerKind kind, double lat, double lon,
        string? city = null, string source = "openstreetmap", string? sourceId = null) => new()
    {
        Source = source,
        SourceId = sourceId ?? $"node/{Guid.NewGuid():N}",
        Name = name,
        Kind = kind,
        City = city,
        Region = "BC",
        Latitude = lat,
        Longitude = lon,
        RetrievedAt = DateTimeOffset.UtcNow
    };

    private async Task AddShopsAsync(params Retailer[] shops)
    {
        await using var db = fixture.CreateDbContext();
        db.Retailers.AddRange(shops);
        await db.SaveChangesAsync();
    }

    // Relevance depends on the real category slugs (flowers, trees, …), which come from the development seed script;
    // it is safe to run again, so the plant joins a seeded group of that category.
    private async Task<Plant> CreatePlantAsync(string categorySlug, PlantStatus status = PlantStatus.Published)
    {
        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand(SeedScript, connection);
            await seed.ExecuteNonQueryAsync();
        }

        await using var db = fixture.CreateDbContext();
        var groupId = await db.PlantGroups.Where(g => g.Category!.Slug == categorySlug).Select(g => g.Id).FirstAsync();
        var plant = TestData.NewPlant(TestData.NewPlantGroup(TestData.NewCategory()), "Test plant", status);
        plant.PlantGroup = null;
        plant.PlantGroupId = groupId;
        db.Plants.Add(plant);
        await db.SaveChangesAsync();
        return plant;
    }

    private static readonly string SeedScript =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DevData", "seed-dev-data.sql"));

    private async Task<JsonDocument> GetAsync(string url)
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static List<string?> Names(JsonDocument json) =>
        json.RootElement.GetProperty("data").EnumerateArray().Select(r => r.GetProperty("name").GetString()).ToList();
}
