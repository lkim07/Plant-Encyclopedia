using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.ExternalData.Images;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;
using SkiaSharp;

namespace PlantEncyclopedia.Infrastructure.ExternalData.Images;

/// <summary>
/// Downloads an accepted image and stores it as WebP in three widths (ARCHITECTURE §22):
/// <list type="bullet">
/// <item>HTTPS from the image hosts of the sources only; no redirects, no retries; 15-second timeout; 15 MB limit.</item>
/// <item>The bytes must decode as JPEG, PNG or WebP within 12,000 pixels a side and 50 megapixels.</item>
/// <item>The pixels are re-encoded, so camera data such as GPS coordinates is not kept. EXIF orientation is applied.</item>
/// <item>Widths 400, 800 and 1600 (never enlarged), WebP quality 82.</item>
/// </list>
/// Every failure is logged without the URL and returns null. A used-up image budget is not a failure: it
/// propagates as <see cref="ImageCallBudgetExhaustedException"/> so the worker can try again later.
/// </summary>
internal sealed partial class ImageFileStore(HttpClient http, IImageStorage storage, ILogger<ImageFileStore> logger) : IImageFileStore
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    public const int MaxDownloadBytes = 15 * 1024 * 1024;
    public const int MaxSide = 12_000;
    public const long MaxPixels = 50_000_000;
    public const int WebpQuality = 82;

    public static readonly IReadOnlyList<(string Name, int Width)> Sizes = [("thumb", 400), ("medium", 800), ("large", 1600)];

    private static readonly HashSet<string> Hosts =
        ["inaturalist-open-data.s3.amazonaws.com", "upload.wikimedia.org", "thumb.wikimedia.org"];

    private static readonly HashSet<string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public static void ConfigureClient(HttpClient client, GbifOptions options)
    {
        client.Timeout = Timeout;
        client.MaxResponseContentBufferSize = MaxDownloadBytes;

        var version = typeof(ImageFileStore).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PlantEncyclopedia", version));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue($"(+{options.Contact})"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
    }

    public async Task<StoredImageFiles?> SaveAsync(Guid plantId, Guid imageId, AcceptedImage image, CancellationToken cancellationToken)
    {
        var url = image.Image.DownloadUrl;
        if (url.Scheme != Uri.UriSchemeHttps || !Hosts.Contains(url.Host))
        {
            LogHostNotAllowed(logger);
            return null;
        }

        var bytes = await DownloadAsync(url, cancellationToken);
        if (bytes is null) return null;

        var renditions = Process(bytes);
        if (renditions is null)
        {
            LogNotAnImage(logger);
            return null;
        }

        var urls = new List<string>();
        foreach (var rendition in renditions)
        {
            urls.Add(await storage.SaveAsync($"plants/{plantId:N}/{imageId:N}-{rendition.Name}.webp", rendition.Webp, cancellationToken));
        }

        var large = renditions[^1];
        return new StoredImageFiles(urls[0], urls[1], urls[2], large.Width, large.Height);
    }

    private async Task<byte[]?> DownloadAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            // The default completion option buffers the body, enforcing MaxResponseContentBufferSize.
            using var response = await http.GetAsync(url, cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                LogUnexpectedStatus(logger, (int)response.StatusCode);
                return null;
            }

            if (response.Content.Headers.ContentType?.MediaType is not { } type || !ContentTypes.Contains(type))
            {
                LogUnexpectedContentType(logger);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogTimedOut(logger, Timeout.TotalSeconds);
            return null;
        }
        catch (HttpRequestException e)
        {
            // Not logged as an exception: its message could include response content.
            LogRequestFailed(logger, e.HttpRequestError);
            return null;
        }
    }

    /// <summary>The three WebP renditions, smallest first, or null when the bytes are not an acceptable image.</summary>
    internal static IReadOnlyList<(string Name, byte[] Webp, int Width, int Height)>? Process(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null) return null;
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp)) return null;

        // Checked from the header, before any pixels are decoded.
        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (width <= 0 || height <= 0 || width > MaxSide || height > MaxSide || (long)width * height > MaxPixels) return null;

        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null) return null;

        var oriented = Orient(decoded, codec.EncodedOrigin);
        try
        {
            var renditions = new List<(string, byte[], int, int)>();
            foreach (var (name, maxWidth) in Sizes)
            {
                var targetWidth = Math.Min(maxWidth, oriented.Width);
                var targetHeight = Math.Max(1, (int)Math.Round(oriented.Height * (double)targetWidth / oriented.Width));

                using var resized = oriented.Resize(
                    new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul),
                    new SKSamplingOptions(SKCubicResampler.Mitchell));
                if (resized is null) return null;

                using var skImage = SKImage.FromBitmap(resized);
                using var encoded = skImage.Encode(SKEncodedImageFormat.Webp, WebpQuality);
                if (encoded is null) return null;

                renditions.Add((name, encoded.ToArray(), targetWidth, targetHeight));
            }

            return renditions;
        }
        finally
        {
            if (!ReferenceEquals(oriented, decoded)) oriented.Dispose();
        }
    }

    // Draws the pixels upright according to the EXIF orientation; the decoder itself does not rotate.
    private static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return bitmap;

        var (w, h) = (bitmap.Width, bitmap.Height);
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(swap ? h : w, swap ? w : h, bitmap.ColorType, bitmap.AlphaType);

        using var canvas = new SKCanvas(result);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: canvas.Translate(w, 0); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight: canvas.Translate(w, h); canvas.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft: canvas.Translate(0, h); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(h, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: canvas.Translate(h, w); canvas.RotateDegrees(-90); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, w); canvas.RotateDegrees(-90); break;
        }

        canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return result;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image download skipped: the host is not an allowed image host.")]
    private static partial void LogHostNotAllowed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image download returned HTTP {StatusCode}.")]
    private static partial void LogUnexpectedStatus(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image download returned a type other than JPEG, PNG or WebP.")]
    private static partial void LogUnexpectedContentType(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image download timed out after {Seconds} s.")]
    private static partial void LogTimedOut(ILogger logger, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image download failed ({Error}).")]
    private static partial void LogRequestFailed(ILogger logger, HttpRequestError error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Downloaded file is not a usable image (type, size or data).")]
    private static partial void LogNotAnImage(ILogger logger);
}
