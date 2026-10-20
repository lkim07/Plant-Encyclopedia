using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace PlantEncyclopedia.Infrastructure.ExternalData.Images;

/// <summary>The outcome of <see cref="R2ConnectionCheck.CheckR2Async"/>; never contains keys.</summary>
public sealed record R2CheckResult(bool Uploaded, bool PublicReadMatches, bool Deleted, string PublicUrl, string? Problem);

public static class R2ConnectionCheck
{
    /// <summary>
    /// Uploads a tiny generated WebP to checks/{guid}.webp, reads it back through the public address, compares the bytes
    /// and deletes it. Proves the keys, bucket and public address work together without touching plant images.
    /// </summary>
    public static async Task<R2CheckResult> CheckR2Async(this IServiceProvider services, CancellationToken cancellationToken)
    {
        var storage = services.GetService<R2ImageStorage>()
            ?? throw new InvalidOperationException(
                $"Image storage is not R2. Set {PlantImageOptions.SectionName}:Enabled to true and Storage to R2 first.");

        var key = $"checks/{Guid.NewGuid():N}.webp";
        var bytes = TinyWebp();
        string url;
        try
        {
            url = await storage.SaveAsync(key, bytes, cancellationToken);
        }
        catch (Exception e)
        {
            return new R2CheckResult(false, false, false, "", $"Upload failed: {e.GetType().Name}: {e.Message}");
        }

        bool matches;
        string? problem = null;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            using var response = await http.GetAsync(url, cancellationToken);
            var body = response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(cancellationToken) : [];
            matches = body.AsSpan().SequenceEqual(bytes);
            if (!matches) problem = $"Public read returned HTTP {(int)response.StatusCode} with different content.";
        }
        catch (Exception e)
        {
            matches = false;
            problem = $"Public read failed: {e.GetType().Name}: {e.Message}";
        }

        var deleted = true;
        try
        {
            await storage.DeleteAsync(key, cancellationToken);
        }
        catch (Exception)
        {
            deleted = false;
        }

        return new R2CheckResult(true, matches, deleted, url, problem);
    }

    private static byte[] TinyWebp()
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(new SKColor(0x7A, 0x52, 0x32));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 80);
        return data.ToArray();
    }
}
