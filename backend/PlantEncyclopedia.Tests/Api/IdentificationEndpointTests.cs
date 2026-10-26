using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlantEncyclopedia.Api.Identification;
using PlantEncyclopedia.Application.Identification;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Tests.Identification;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// POST /api/identification (API_SPEC §36–42) with a fake provider: only Published encyclopedia plants come back,
/// ranked and without scores; uploads are checked; failures are recoverable; cost protections hold.
/// </summary>
[Collection(PostgresCollection.Name)]
public class IdentificationEndpointTests(PostgresDatabaseFixture fixture)
{
    private const string Url = "/api/identification";

    // Protects against: AI suggestions shown as encyclopedia results without a matching plant (D-011), drafts shown,
    // more than three candidates, or confidence values in the response (D-013).
    [Fact]
    public async Task Identify_ReturnsOnlyPublishedMatches_InTheProvidersOrder()
    {
        var (species, cultivar) = await CreateSpeciesWithCultivarAsync();
        var other = await CreatePlantAsync("Sweet briar");
        var draft = await CreatePlantAsync("Draft", PlantStatus.Draft);
        var provider = new FakeProvider(ProviderIdentification.Answered(
        [
            new(other.ScientificName, 0.7),
            new(draft.ScientificName, 0.65),
            new("Unknownus imaginarius", 0.6),
            new(species.ScientificName, 0.5)
        ]));
        await using var factory = Factory(provider);
        var browser = await TestBrowser.OpenAsync(factory);

        var response = await browser.SendContentAsync(HttpMethod.Post, Url, Photo(TestPhotos.Jpeg()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("completed", data.GetProperty("status").GetString());
        var candidates = data.GetProperty("candidates").EnumerateArray().ToList();
        Assert.Equal([other.Id, species.Id, cultivar.Id], candidates.Select(c => c.GetProperty("plant").GetProperty("id").GetGuid()));
        Assert.Equal([1, 2, 3], candidates.Select(c => c.GetProperty("rank").GetInt32()));
        Assert.Equal(
            $$$"""{"rank":1,"plant":{"id":"{{{other.Id}}}","name":"Sweet briar","plantGroup":"Roses","plantGroupId":"{{{other.PlantGroup!.Slug}}}","scientificName":"{{{other.ScientificName}}}","imageUrl":null}}""",
            candidates[0].GetRawText());
        Assert.Equal(1, provider.Calls);
    }

    // Protects against: claiming a plant does not exist, or a dead end, when nothing matched (UX §44).
    [Fact]
    public async Task Identify_WithNoMatchingPlant_FailsRecoverably()
    {
        await using var factory = Factory(new FakeProvider(ProviderIdentification.Answered([new("Unknownus imaginarius", 0.9)])));
        var browser = await TestBrowser.OpenAsync(factory);

        var response = await browser.SendContentAsync(HttpMethod.Post, Url, Photo(TestPhotos.Jpeg()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("IDENTIFICATION_FAILED", await response.Content.ReadAsStringAsync());
    }

    // Protects against: a missing or failing provider looking like "no such plant". Tests never configure Pl@ntNet.
    [Fact]
    public async Task Identify_WithoutAProvider_IsUnavailable()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var browser = await TestBrowser.OpenAsync(factory);

        var response = await browser.SendContentAsync(HttpMethod.Post, Url, Photo(TestPhotos.Jpeg()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("IDENTIFICATION_UNAVAILABLE", await response.Content.ReadAsStringAsync());
    }

    // Protects against: arbitrary or oversized files reaching the provider (SECURITY §10).
    [Fact]
    public async Task Uploads_AreChecked_BeforeTheProviderIsAsked()
    {
        var provider = new FakeProvider(ProviderIdentification.Answered([]));
        await using var factory = Factory(provider);
        var browser = await TestBrowser.OpenAsync(factory);

        var text = await browser.SendContentAsync(HttpMethod.Post, Url, Photo("not a photo"u8.ToArray(), fileName: "plant.jpg"));
        var noFile = await browser.SendContentAsync(HttpMethod.Post, Url, new MultipartFormDataContent { { new StringContent("x"), "other" } });
        var json = await browser.PostAsync(Url, new { image = "x" });
        var tooLarge = await browser.SendContentAsync(HttpMethod.Post, Url, Photo(new byte[11 * 1024 * 1024]));

        Assert.Equal(HttpStatusCode.BadRequest, text.StatusCode);
        Assert.Contains("\"image\"", await text.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, noFile.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, json.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        Assert.Equal(0, provider.Calls);
    }

    // Protects against: other sites spending the identification budget through visitors' browsers.
    [Fact]
    public async Task Identify_NeedsTheCsrfToken()
    {
        var provider = new FakeProvider(ProviderIdentification.Answered([]));
        await using var factory = Factory(provider);
        var browser = await TestBrowser.OpenAsync(factory);

        var response = await browser.Client.PostAsync(Url, Photo(TestPhotos.Jpeg()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, provider.Calls);
    }

    // Protects against: one client using up the shared daily budget (API_SPEC §40).
    [Fact]
    public async Task Identify_IsRateLimitedPerClient()
    {
        var provider = new FakeProvider(ProviderIdentification.Answered([]));
        await using var factory = Factory(provider);
        var browser = await TestBrowser.OpenAsync(factory);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i <= IdentificationEndpoints.RequestsPerMinute; i++)
        {
            statuses.Add((await browser.SendContentAsync(HttpMethod.Post, Url, Photo(TestPhotos.Jpeg(200, 200)))).StatusCode);
        }

        Assert.All(statuses.Take(IdentificationEndpoints.RequestsPerMinute), s => Assert.Equal(HttpStatusCode.UnprocessableEntity, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    private static MultipartFormDataContent Photo(byte[] bytes, string fileName = "photo.jpg")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new MultipartFormDataContent { { file, "image", fileName } };
    }

    private WebApplicationFactory<Program> Factory(IPlantIdentificationProvider provider) =>
        new TestApiFactory(fixture.ConnectionString).WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddSingleton(provider)));

    private async Task<Plant> CreatePlantAsync(string name, PlantStatus status = PlantStatus.Published)
    {
        await using var db = fixture.CreateDbContext();
        var plant = TestData.NewPlant(TestData.NewPlantGroup(TestData.NewCategory(), "Roses"), name, status);
        db.Plants.Add(plant);
        await db.SaveChangesAsync();
        return plant;
    }

    private async Task<(Plant Species, Plant Cultivar)> CreateSpeciesWithCultivarAsync()
    {
        await using var db = fixture.CreateDbContext();
        var group = TestData.NewPlantGroup(TestData.NewCategory(), "Roses");
        var species = TestData.NewPlant(group, "Dog rose");
        var cultivar = TestData.NewPlant(group, "Dog rose 'Abbotswood'");
        cultivar.ScientificName = species.ScientificName;
        cultivar.CultivarName = "Abbotswood";
        db.Plants.AddRange(species, cultivar);
        await db.SaveChangesAsync();
        return (species, cultivar);
    }

    private sealed class FakeProvider(ProviderIdentification answer) : IPlantIdentificationProvider
    {
        private int _calls;

        public int Calls => _calls;

        public Task<ProviderIdentification> IdentifyAsync(byte[] jpeg, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(answer);
        }
    }
}
