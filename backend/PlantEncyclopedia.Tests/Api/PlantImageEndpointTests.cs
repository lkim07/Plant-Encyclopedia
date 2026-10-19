using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>Stored plant images in API responses (API_SPEC §14, §19, §25) and the /media file endpoint.</summary>
[Collection(PostgresCollection.Name)]
public class PlantImageEndpointTests(PostgresDatabaseFixture fixture)
{
    // Protects against: an image shown without the credit its licence requires, or a drawing preferred over a
    // photo. A drawing stored next to a photo is not returned at all.
    [Fact]
    public async Task Detail_ReturnsThePhotoAsHero_WithItsCredit()
    {
        var plant = await CreatePlantAsync(Photo(), Illustration());
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/plants/{plant.Id}"));
        var data = json.RootElement.GetProperty("data");

        const string photo =
            """{"url":"/media/photo-large.webp","altText":"Dog rose (Rosa canina)","kind":"photo","credit":{"author":"Douglas Goldman","license":"CC BY-SA","licenseUrl":null,"source":"iNaturalist","sourceUrl":null}}""";
        Assert.Equal(photo, data.GetProperty("heroImage").GetRawText());
        Assert.False(data.TryGetProperty("illustration", out _));
    }

    // Protects against: a plant with only a drawing showing a placeholder, or the drawing uncredited.
    [Fact]
    public async Task Detail_UsesTheIllustrationAsHero_WhenThereIsNoPhoto()
    {
        var plant = await CreatePlantAsync(Illustration());
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/plants/{plant.Id}"));
        var data = json.RootElement.GetProperty("data");

        const string drawing =
            """{"url":"/media/drawing-large.webp","altText":"Botanical illustration of Dog rose (Rosa canina)","kind":"illustration","credit":{"author":"Franz Bley","license":"Public domain","licenseUrl":null,"source":"Wikimedia Commons","sourceUrl":"https://commons.wikimedia.org/wiki/File:Bley.jpg"}}""";
        Assert.Equal(drawing, data.GetProperty("heroImage").GetRawText());
    }

    // Protects against: crediting an image with a licence the import would never accept, or an unreviewed source.
    [Fact]
    public async Task Detail_OmitsCredit_ForUnknownSourcesOrLicences()
    {
        var unknownSource = Photo();
        unknownSource.Source = "unreviewed-provider";
        var plant = await CreatePlantAsync(unknownSource);
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/plants/{plant.Id}"));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").GetProperty("heroImage").GetProperty("credit").ValueKind);
    }

    // Protects against: cards without images when the plant has one, or a drawing preferred over a photo.
    [Fact]
    public async Task Cards_UseThePhoto_ElseTheIllustration()
    {
        var withBoth = await CreatePlantAsync(Illustration(), Photo());
        var drawingOnly = await CreatePlantAsync(Illustration());
        var none = await CreatePlantAsync();
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal("/media/photo-medium.webp", await SearchCardImageAsync(client, withBoth));
        Assert.Equal("/media/drawing-medium.webp", await SearchCardImageAsync(client, drawingOnly));
        Assert.Null(await SearchCardImageAsync(client, none));

        using var group = JsonDocument.Parse(await client.GetStringAsync($"/api/plant-groups/{withBoth.PlantGroup!.Slug}"));
        Assert.Equal("/media/photo-medium.webp",
            group.RootElement.GetProperty("data").GetProperty("plants")[0].GetProperty("imageUrl").GetString());
    }

    // Protects against: stored images not being served, being served as another type, or files outside the
    // image folder (or of other types) being reachable.
    [Fact]
    public async Task Media_ServesOnlyWebpFilesFromTheImageFolder()
    {
        var root = Directory.CreateTempSubdirectory("plant-images-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "plants"));
            await File.WriteAllBytesAsync(Path.Combine(root, "plants", "a.webp"), [1, 2, 3]);
            await File.WriteAllTextAsync(Path.Combine(root, "plants", "notes.txt"), "secret");
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(root)!, "outside.webp"), "outside");

            await using var factory = new TestApiFactory(fixture.ConnectionString);
            using var client = factory.WithWebHostBuilder(b => b.UseSetting("ExternalPlantData:Images:StoragePath", root)).CreateClient();

            var image = await client.GetAsync("/media/plants/a.webp");
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal("image/webp", image.Content.Headers.ContentType?.MediaType);
            Assert.Equal("public, max-age=31536000, immutable", image.Headers.CacheControl?.ToString());
            Assert.Equal("nosniff", image.Headers.GetValues("X-Content-Type-Options").Single());

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/media/plants/notes.txt")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/media/../outside.webp")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/media/%2e%2e/outside.webp")).StatusCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            File.Delete(Path.Combine(Path.GetDirectoryName(root)!, "outside.webp"));
        }
    }

    private static async Task<string?> SearchCardImageAsync(HttpClient client, Plant plant)
    {
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/search?q=" + Uri.EscapeDataString(plant.DisplayName)));
        var result = json.RootElement.GetProperty("data").EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == plant.Id);
        return result.GetProperty("imageUrl").GetString();
    }

    private static PlantImage Photo() => new()
    {
        Kind = PlantImageKind.Photo,
        Url = "/media/photo-large.webp",
        ThumbnailUrl = "/media/photo-thumb.webp",
        MediumUrl = "/media/photo-medium.webp",
        LargeUrl = "/media/photo-large.webp",
        Width = 1600,
        Height = 2133,
        Source = "inaturalist",
        SourceId = Guid.NewGuid().ToString("N"),
        License = "cc-by-sa",
        Attribution = "Douglas Goldman",
        AltText = "Dog rose (Rosa canina)",
        RetrievedAt = DateTimeOffset.UtcNow
    };

    private static PlantImage Illustration() => new()
    {
        Kind = PlantImageKind.Illustration,
        Url = "/media/drawing-large.webp",
        ThumbnailUrl = "/media/drawing-thumb.webp",
        MediumUrl = "/media/drawing-medium.webp",
        LargeUrl = "/media/drawing-large.webp",
        Width = 1600,
        Height = 2400,
        Source = "wikimedia-commons",
        SourceId = "File:Bley.jpg",
        SourceUrl = "https://commons.wikimedia.org/wiki/File:Bley.jpg",
        License = "pd",
        Attribution = "Franz Bley",
        AltText = "Botanical illustration of Dog rose (Rosa canina)",
        RetrievedAt = DateTimeOffset.UtcNow
    };

    private async Task<Plant> CreatePlantAsync(params PlantImage[] images)
    {
        await using var db = fixture.CreateDbContext();
        var plant = TestData.NewPlant(TestData.NewPlantGroup(TestData.NewCategory()), $"Imaged plant {Guid.NewGuid():N}");
        plant.Images.AddRange(images);
        db.Plants.Add(plant);
        await db.SaveChangesAsync();
        return plant;
    }
}
