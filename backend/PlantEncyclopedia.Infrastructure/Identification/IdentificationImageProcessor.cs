using PlantEncyclopedia.Application.Identification;
using PlantEncyclopedia.Infrastructure.ExternalData.Images;
using SkiaSharp;

namespace PlantEncyclopedia.Infrastructure.Identification;

/// <summary>
/// Checks an uploaded photo from its bytes (never its name or declared type) and makes the copy sent to the provider
/// (SECURITY §10–11): JPEG, PNG or WebP only; size checked from the header before decoding; large photos decoded at a
/// reduced scale where the format allows, so a phone photo never needs hundreds of MB; drawn upright; shrunk to at
/// most <see cref="MaxSide"/> pixels; and re-encoded as JPEG, which leaves out all metadata, location included.
/// The upload itself is never stored.
/// </summary>
internal sealed class IdentificationImageProcessor : IIdentificationImageProcessor
{
    /// <summary>Longest side of the copy sent to the provider.</summary>
    public const int MaxSide = 1280;

    public const int MinSide = 64;
    public const int MaxSourceSide = 16_000;
    public const long MaxSourcePixels = 100_000_000;

    /// <summary>Photos larger than this are only accepted when the decoder can scale them down while decoding.</summary>
    public const long MaxFullDecodePixels = 25_000_000;

    public const int JpegQuality = 85;

    public byte[]? Prepare(byte[] upload)
    {
        using var data = SKData.CreateCopy(upload);
        using var codec = SKCodec.Create(data);
        if (codec is null) return null;
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp)) return null;

        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (Math.Min(width, height) < MinSide || Math.Max(width, height) > MaxSourceSide || (long)width * height > MaxSourcePixels) return null;

        using var decoded = Decode(codec, width, height);
        if (decoded is null) return null;

        var oriented = ImageFileStore.Orient(decoded, codec.EncodedOrigin);
        try
        {
            var scale = Math.Min(1.0, (double)MaxSide / Math.Max(oriented.Width, oriented.Height));
            var target = new SKImageInfo(
                Math.Max(1, (int)Math.Round(oriented.Width * scale)),
                Math.Max(1, (int)Math.Round(oriented.Height * scale)),
                SKColorType.Rgba8888,
                SKAlphaType.Premul);

            using var resized = oriented.Resize(target, new SKSamplingOptions(SKCubicResampler.Mitchell));
            if (resized is null) return null;

            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
            return encoded?.ToArray();
        }
        finally
        {
            if (!ReferenceEquals(oriented, decoded)) oriented.Dispose();
        }
    }

    // Asks the decoder for roughly twice the final size (JPEG and WebP decoders scale natively); falls back to a full
    // decode only for photos small enough to decode whole.
    private static SKBitmap? Decode(SKCodec codec, int width, int height)
    {
        var wanted = Math.Min(1f, 2f * MaxSide / Math.Max(width, height));
        var scaled = codec.GetScaledDimensions(wanted);
        if (scaled.Width < width)
        {
            var info = new SKImageInfo(scaled.Width, scaled.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);
            if (codec.GetPixels(info, bitmap.GetPixels()) == SKCodecResult.Success) return bitmap;
            bitmap.Dispose();
        }

        if ((long)width * height > MaxFullDecodePixels) return null;
        var full = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var whole = new SKBitmap(full);
        if (codec.GetPixels(full, whole.GetPixels()) == SKCodecResult.Success) return whole;
        whole.Dispose();
        return null;
    }
}
