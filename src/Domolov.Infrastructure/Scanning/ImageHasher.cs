using Domolov.Domain.Homes;
using SkiaSharp;

namespace Domolov.Infrastructure.Scanning;

/// <summary>Computes perceptual photo hashes with SkiaSharp.</summary>
public static class ImageHasher
{
    /// <summary>Decodes an image, shrinks it to 9x8 grayscale and returns its dHash, or null.</summary>
    public static long? Compute(byte[] imageBytes)
    {
        if (imageBytes.Length == 0)
        {
            return null;
        }

        using var codec = SKCodec.Create(new SKMemoryStream(imageBytes));
        if (codec is null)
        {
            return null;
        }

        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null)
        {
            return null;
        }

        var info = new SKImageInfo(
            PerceptualHash.Width,
            PerceptualHash.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul
        );
        using var small = bitmap.Resize(
            info,
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
        );
        if (small is null)
        {
            return null;
        }

        Span<byte> gray = stackalloc byte[PerceptualHash.Width * PerceptualHash.Height];
        for (var y = 0; y < PerceptualHash.Height; y++)
        {
            for (var x = 0; x < PerceptualHash.Width; x++)
            {
                var c = small.GetPixel(x, y);
                gray[(y * PerceptualHash.Width) + x] = (byte)(
                    (0.299 * c.Red) + (0.587 * c.Green) + (0.114 * c.Blue)
                );
            }
        }

        return PerceptualHash.DHash(gray);
    }
}
