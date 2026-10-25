using SkiaSharp;

namespace PlantEncyclopedia.Tests.Identification;

/// <summary>Photos made in memory for identification tests.</summary>
internal static class TestPhotos
{
    public static byte[] Jpeg(int width = 1600, int height = 1200) => Encode(width, height, SKEncodedImageFormat.Jpeg);

    public static byte[] Png(int width = 800, int height = 600) => Encode(width, height, SKEncodedImageFormat.Png);

    /// <summary>
    /// A JPEG with an EXIF block (APP1) holding an orientation and a recognizable marker standing in for private
    /// metadata such as GPS coordinates.
    /// </summary>
    public static byte[] JpegWithExif(int width, int height, ushort orientation, string marker)
    {
        var jpeg = Jpeg(width, height);

        // TIFF header (big-endian), one IFD entry: Orientation (0x0112), SHORT, count 1, value; then no next IFD.
        var tiff = new List<byte> { (byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8, 0, 1, 0x01, 0x12, 0, 3, 0, 0, 0, 1 };
        tiff.AddRange([(byte)(orientation >> 8), (byte)orientation, 0, 0, 0, 0, 0, 0]);
        tiff.AddRange(System.Text.Encoding.ASCII.GetBytes(marker));

        var payload = new List<byte>();
        payload.AddRange("Exif\0\0"u8.ToArray());
        payload.AddRange(tiff);
        var length = payload.Count + 2;

        var app1 = new List<byte> { 0xFF, 0xE1, (byte)(length >> 8), (byte)length };
        app1.AddRange(payload);

        // Right after the start-of-image marker (FF D8).
        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
    }

    public static (int Width, int Height) SizeOf(byte[] image)
    {
        using var codec = SKCodec.Create(SKData.CreateCopy(image));
        return (codec.Info.Width, codec.Info.Height);
    }

    public static SKEncodedImageFormat FormatOf(byte[] image)
    {
        using var codec = SKCodec.Create(SKData.CreateCopy(image));
        return codec.EncodedFormat;
    }

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(60, 140, 70));
            using var paint = new SKPaint { Color = new SKColor(200, 60, 90) };
            canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
