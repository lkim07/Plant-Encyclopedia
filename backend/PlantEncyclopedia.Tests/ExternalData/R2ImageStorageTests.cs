using System.Collections.Concurrent;
using System.Net;
using Amazon.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Infrastructure.ExternalData.Images;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.ExternalData;

/// <summary>
/// Cloudflare R2 storage through its S3 API (Phase 11d-3). Offline: the S3 client's HTTP requests are answered in
/// memory by <see cref="FakeR2"/>, so nothing reaches Cloudflare and no real keys are used.
/// </summary>
public class R2ImageStorageTests
{
    internal const string AccountId = "0123456789abcdef0123456789abcdef";

    internal static PlantImageOptions Options(string? storagePath = null) => new()
    {
        Enabled = true,
        Storage = ImageStorageKind.R2,
        StoragePath = storagePath ?? Path.GetTempPath(),
        R2 = new R2Options
        {
            AccountId = AccountId,
            AccessKeyId = "test-key-id",
            SecretAccessKey = "test-secret",
            Bucket = "plant-encyclopedia-images",
            PublicBaseUrl = "https://pub-test.r2.dev/"
        }
    };

    // Protects against: uploading to the wrong endpoint or bucket, files the browser would cache briefly or treat
    // as another type, unsigned requests, or URLs that do not point at the public bucket address.
    [Fact]
    public async Task Save_PutsTheFileInTheBucket_AndReturnsItsPublicUrl()
    {
        var r2 = new FakeR2();
        using var storage = new R2ImageStorage(Options(), r2);

        var url = await storage.SaveAsync("plants/abc/def-large.webp", [1, 2, 3], CancellationToken.None);

        Assert.Equal("https://pub-test.r2.dev/plants/abc/def-large.webp", url);
        var put = Assert.Single(r2.Requests);
        Assert.Equal(HttpMethod.Put, put.Method);
        Assert.Equal($"https://{AccountId}.r2.cloudflarestorage.com/plant-encyclopedia-images/plants/abc/def-large.webp", put.Url);
        Assert.Equal("image/webp", put.ContentType);
        Assert.Equal(R2ImageStorage.CacheControl, put.CacheControl);
        Assert.StartsWith("AWS4-HMAC-SHA256 Credential=test-key-id/", put.Authorization);
        Assert.Contains("/auto/s3/aws4_request", put.Authorization);
        Assert.DoesNotContain("test-secret", put.Authorization);
        Assert.Equal([1, 2, 3], put.Body);
    }

    [Theory]
    [InlineData("../outside.webp")]
    [InlineData("plants/../../outside.webp")]
    [InlineData("/plants/a.webp")]
    [InlineData("plants\\a.webp")]
    public async Task Save_RefusesKeysThatAreNotPlainRelativePaths(string key)
    {
        var r2 = new FakeR2();
        using var storage = new R2ImageStorage(Options(), r2);

        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(key, [1], CancellationToken.None));
        Assert.Empty(r2.Requests);
    }

    // Protects against: an upload failure being reported as success (the image row would point at nothing).
    [Fact]
    public async Task Save_Throws_WhenR2RefusesTheUpload()
    {
        var r2 = new FakeR2(HttpStatusCode.Forbidden);
        using var storage = new R2ImageStorage(Options(), r2);

        await Assert.ThrowsAnyAsync<Exception>(() => storage.SaveAsync("plants/a/b.webp", [1], CancellationToken.None));
    }

    // Protects against: starting with missing keys, a mistyped account, plain-HTTP public URLs or URLs with credentials.
    [Theory]
    [InlineData("AccountId", "not-an-account")]
    [InlineData("AccessKeyId", "")]
    [InlineData("SecretAccessKey", null)]
    [InlineData("Bucket", "Bad_Bucket")]
    [InlineData("PublicBaseUrl", "http://pub-test.r2.dev")]
    [InlineData("PublicBaseUrl", "https://user:pass@pub-test.r2.dev")]
    [InlineData("PublicBaseUrl", null)]
    public void Options_RejectIncompleteOrUnsafeSettings(string property, string? value)
    {
        var options = Options();
        typeof(R2Options).GetProperty(property)!.SetValue(options.R2, value);

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Options_AcceptCompleteSettings() => Options().Validate();

    /// <summary>Answers the S3 client's requests in memory and records them.</summary>
    internal sealed class FakeR2(HttpStatusCode status = HttpStatusCode.OK) : HttpClientFactory
    {
        public ConcurrentQueue<Recorded> Requests { get; } = new();

        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(new Handler(this, status));

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;

        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => true;

        internal sealed record Recorded(
            HttpMethod Method,
            string Url,
            string? ContentType,
            string? CacheControl,
            string? Authorization,
            byte[] Body);

        private sealed class Handler(FakeR2 owner, HttpStatusCode status) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner.Requests.Enqueue(new Recorded(
                    request.Method,
                    request.RequestUri!.AbsoluteUri,
                    request.Content?.Headers.ContentType?.MediaType,
                    Header(request, "Cache-Control"),
                    Header(request, "Authorization"),
                    request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken)));

                var response = new HttpResponseMessage(status) { Content = new StringContent("") };
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"etag\"");
                return response;
            }

            // The SDK adds headers without validation, on the request or on its content.
            private static string? Header(HttpRequestMessage request, string name)
            {
                if (request.Headers.TryGetValues(name, out var values)) return string.Join(",", values);
                return request.Content is not null && request.Content.Headers.TryGetValues(name, out var content)
                    ? string.Join(",", content)
                    : null;
            }
        }
    }
}

/// <summary>The one-off move of local images to R2, on the disposable test database with a fake R2.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ImageStorageMigrationTests(PostgresDatabaseFixture fixture) : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("plant-images-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Protects against: images left pointing at the local folder, files uploaded twice, or a crash on a missing file.
    [Fact]
    public async Task Migrate_UploadsLocalFiles_RewritesUrls_AndCanRunAgain()
    {
        var folder = Path.Combine(_root, "plants", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var name = Path.GetFileName(folder);
        foreach (var size in new[] { "thumb", "medium", "large" })
        {
            await File.WriteAllBytesAsync(Path.Combine(folder, $"img-{size}.webp"), [7]);
        }

        Guid imageId, missingId;
        await using (var db = fixture.CreateDbContext())
        {
            var plant = TestData.NewPlant();
            var image = Row($"/media/plants/{name}/img");
            var gone = Row($"/media/plants/{name}/gone");
            plant.Images.AddRange([image, gone]);
            db.Plants.Add(plant);
            await db.SaveChangesAsync();
            (imageId, missingId) = (image.Id, gone.Id);
        }

        var r2 = new R2ImageStorageTests.FakeR2();
        var first = await MigrateAsync(r2);

        await using (var db = fixture.CreateDbContext())
        {
            var moved = await db.PlantImages.SingleAsync(i => i.Id == imageId);
            Assert.Equal(
                ($"https://pub-test.r2.dev/plants/{name}/img-thumb.webp", $"https://pub-test.r2.dev/plants/{name}/img-medium.webp",
                 $"https://pub-test.r2.dev/plants/{name}/img-large.webp", $"https://pub-test.r2.dev/plants/{name}/img-large.webp"),
                (moved.ThumbnailUrl, moved.MediumUrl, moved.LargeUrl, moved.Url));
            Assert.StartsWith("/media/", (await db.PlantImages.SingleAsync(i => i.Id == missingId)).Url);
        }

        Assert.True(first.Files >= 3);
        Assert.True(first.MissingFiles >= 3);
        Assert.Equal(3, r2.Requests.Count(r => r.Url.Contains(name)));

        var again = new R2ImageStorageTests.FakeR2();
        await MigrateAsync(again);
        Assert.DoesNotContain(again.Requests, r => r.Url.Contains($"{name}/img-"));
    }

    private async Task<ImageMigrationResult> MigrateAsync(R2ImageStorageTests.FakeR2 r2)
    {
        await using var db = fixture.CreateDbContext();
        using var storage = new R2ImageStorage(R2ImageStorageTests.Options(_root), r2);
        var migration = new ImageStorageMigration(db, R2ImageStorageTests.Options(_root), storage, NullLogger<ImageStorageMigration>.Instance);
        return await migration.MigrateAsync(CancellationToken.None);
    }

    private static PlantImage Row(string prefix) => new()
    {
        Url = $"{prefix}-large.webp",
        ThumbnailUrl = $"{prefix}-thumb.webp",
        MediumUrl = $"{prefix}-medium.webp",
        LargeUrl = $"{prefix}-large.webp",
        Source = "test",
        SourceId = Guid.NewGuid().ToString("N"),
        RetrievedAt = DateTimeOffset.UtcNow
    };
}
