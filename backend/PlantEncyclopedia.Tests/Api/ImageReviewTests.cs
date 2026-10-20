using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Application.ExternalData.Images;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Infrastructure.ExternalData.Wikimedia;
using PlantEncyclopedia.Infrastructure.Persistence;
using PlantEncyclopedia.Infrastructure.Persistence.Queries;
using PlantEncyclopedia.Tests.ExternalData;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// Rejecting a wrong image (a can instead of the plant), choosing the next one, and the plant-group card images.
/// Nothing goes over the network.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ImageReviewTests(PostgresDatabaseFixture fixture)
{
    // Protects against: crediting Commons' placeholder text as if it were an author (the canned-chile photo).
    [Theory]
    [InlineData("No machine-readable author provided. Carstor assumed (based on copyright claims).", null)]
    [InlineData("<i>No machine-readable author provided</i>", null)]
    [InlineData("Unknown author", null)]
    [InlineData("<span>Unknown author</span><span>Unknown author</span>", null)]
    [InlineData("<a href=\"//commons.wikimedia.org/wiki/User:Roberta_F.\">Roberta F.</a>", "Roberta F.")]
    [InlineData("Unknown authorship of the second edition, scan by NYPL", "Unknown authorship of the second edition, scan by NYPL")]
    public void Commons_PlaceholderAuthors_AreNoAuthor(string html, string? expected) =>
        Assert.Equal(expected, WikimediaClient.Author(html));

    // Protects against: a rejected image still being shown on Plant Detail or cards.
    [Fact]
    public async Task RejectedImages_AreNotShown()
    {
        var plant = await CreatePlantAsync(Image("can", PlantImageKind.Photo, rejected: true), Image("drawing", PlantImageKind.Illustration));
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var detail = JsonDocument.Parse(await client.GetStringAsync($"/api/plants/{plant.Id}"));
        Assert.Equal("/media/drawing-large.webp", detail.RootElement.GetProperty("data").GetProperty("heroImage").GetProperty("url").GetString());

        using var group = JsonDocument.Parse(await client.GetStringAsync($"/api/plant-groups/{plant.PlantGroup!.Slug}"));
        Assert.Equal("/media/drawing-medium.webp", group.RootElement.GetProperty("data").GetProperty("plants")[0].GetProperty("imageUrl").GetString());
    }

    // Protects against: the dev tool rejecting the wrong image, or answering 2xx when there is nothing to reject.
    [Fact]
    public async Task DevEndpoint_RejectsTheHeroImage()
    {
        var plant = await CreatePlantAsync(Image("can", PlantImageKind.Photo), Image("drawing", PlantImageKind.Illustration));
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/dev/plants/{plant.Id}/hero-image/reject", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await using var db = fixture.CreateDbContext();
        var images = await db.PlantImages.Where(i => i.PlantId == plant.Id).ToListAsync();
        Assert.NotNull(images.Single(i => i.SourceId == "can").RejectedAt);
        Assert.Null(images.Single(i => i.SourceId == "drawing").RejectedAt);

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"/api/dev/plants/{plant.Id}/hero-image/reject", null)).StatusCode);
        var noneLeft = await client.PostAsync($"/api/dev/plants/{plant.Id}/hero-image/reject", null);
        Assert.Equal(HttpStatusCode.NotFound, noneLeft.StatusCode);
        Assert.Contains("PLANT_IMAGE_NOT_FOUND", await noneLeft.Content.ReadAsStringAsync());
    }

    // Protects against: the same wrong image being picked again after it was rejected.
    [Fact]
    public async Task Import_SkipsARejectedImage_AndTakesTheNextCandidate()
    {
        var plant = await CreatePlantAsync(Image("photo-1", PlantImageKind.Photo, rejected: true, source: "test"));
        await using var db = fixture.CreateDbContext();
        var service = new PlantImageImportService(
            new PlantImageSelector([new TwoPhotos()]),
            new PlantImageStore(db),
            new StoringFiles(),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.ImportAsync(plant.Id, CancellationToken.None);

        Assert.Equal(1, result.Added);
        var usable = await db.PlantImages.AsNoTracking().SingleAsync(i => i.PlantId == plant.Id && i.RejectedAt == null);
        Assert.Equal("photo-2", usable.SourceId);
    }

    // Protects against: a plant whose only photo was rejected never getting a new one.
    [Fact]
    public async Task Backfill_IncludesPlantsWhosePhotoWasRejected()
    {
        var plant = await CreatePlantAsync(Image("can", PlantImageKind.Photo, rejected: true));
        await using var db = fixture.CreateDbContext();

        Assert.Contains(plant.Id, await new PlantImageStore(db).FindPlantsWithoutImagesAsync(int.MaxValue, CancellationToken.None));
    }

    // Protects against: plant-group cards without images when their plants have some, a drawing preferred over a
    // photo, or a picture that changes on every visit instead of once a day.
    [Fact]
    public async Task GroupCards_ShowOnePlantsImage_StableForADay()
    {
        var category = TestData.NewCategory();
        var roses = TestData.NewPlantGroup(category, "Roses");
        var empty = TestData.NewPlantGroup(category, "Empty");
        await using (var db = fixture.CreateDbContext())
        {
            for (var n = 1; n <= 4; n++)
            {
                var plant = TestData.NewPlant(roses, $"Rose {n}");
                plant.Images.Add(Image($"photo-{n}", PlantImageKind.Photo));
                db.Plants.Add(plant);
            }
            var drawn = TestData.NewPlant(roses, "Drawn rose");
            drawn.Images.Add(Image("drawing", PlantImageKind.Illustration));
            db.Plants.Add(drawn);
            db.PlantGroups.Add(empty);
            await db.SaveChangesAsync();
        }

        var photos = Enumerable.Range(1, 4).Select(n => $"/media/photo-{n}-medium.webp").ToList();
        var day = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
        var picks = new List<string?>();
        for (var d = 0; d < 14; d++)
        {
            var morning = await GroupImageAsync(category.Slug, "Roses", day.AddDays(d));
            var evening = await GroupImageAsync(category.Slug, "Roses", day.AddDays(d).AddHours(14));
            Assert.Equal(morning, evening);
            Assert.Contains(morning, photos);
            picks.Add(morning);
        }

        Assert.True(picks.Distinct().Count() > 1, "The pick should change from day to day.");
        Assert.Null(await GroupImageAsync(category.Slug, "Empty", day));

        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/categories/{category.Slug}"));
        var cards = json.RootElement.GetProperty("data").GetProperty("plantGroups").EnumerateArray().ToList();
        Assert.Contains(cards.Single(c => c.GetProperty("name").GetString() == "Roses").GetProperty("imageUrl").GetString(), photos);
    }

    private async Task<string?> GroupImageAsync(string categorySlug, string groupName, DateTimeOffset now)
    {
        await using var db = fixture.CreateDbContext();
        var detail = await new CategoryQueries(db, new FixedTimeProvider(now)).GetCategoryAsync(categorySlug, CancellationToken.None);
        return detail!.PlantGroups.Single(g => g.Name == groupName).ImageUrl;
    }

    private static PlantImage Image(string id, PlantImageKind kind, bool rejected = false, string source = "inaturalist") => new()
    {
        Kind = kind,
        Url = $"/media/{id}-large.webp",
        MediumUrl = $"/media/{id}-medium.webp",
        LargeUrl = $"/media/{id}-large.webp",
        Source = source,
        SourceId = id,
        License = "cc-by",
        Attribution = "A. Photographer",
        RetrievedAt = DateTimeOffset.UtcNow,
        RejectedAt = rejected ? DateTimeOffset.UtcNow : null
    };

    private async Task<Plant> CreatePlantAsync(params PlantImage[] images)
    {
        await using var db = fixture.CreateDbContext();
        var plant = TestData.NewPlant(TestData.NewPlantGroup(TestData.NewCategory()), $"Reviewed plant {Guid.NewGuid():N}");
        plant.Images.AddRange(images);
        db.Plants.Add(plant);
        await db.SaveChangesAsync();
        return plant;
    }

    /// <summary>Offers "photo-1" (rejected in the test) and then "photo-2".</summary>
    private sealed class TwoPhotos : IPlantImageSource
    {
        public PlantImageKind Kind => PlantImageKind.Photo;

        public Task<IReadOnlyList<ExternalImageCandidate>> GetCandidatesAsync(string scientificName, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExternalImageCandidate>>(
                [PlantImageSelectorTests.Candidate("photo-1"), PlantImageSelectorTests.Candidate("photo-2")]);
    }

    private sealed class StoringFiles : IImageFileStore
    {
        public Task<StoredImageFiles?> SaveAsync(Guid plantId, Guid imageId, AcceptedImage image, CancellationToken cancellationToken) =>
            Task.FromResult<StoredImageFiles?>(new($"/media/{imageId:N}-thumb.webp", $"/media/{imageId:N}-medium.webp", $"/media/{imageId:N}-large.webp", 1600, 1200));
    }
}
