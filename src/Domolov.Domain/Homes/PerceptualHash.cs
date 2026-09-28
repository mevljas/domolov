using System.Numerics;

namespace Domolov.Domain.Homes;

/// <summary>Difference hash (dHash) over a 9x8 grayscale thumbnail.</summary>
public static class PerceptualHash
{
    public const int Width = 9;
    public const int Height = 8;

    /// <summary>
    /// Computes a 64-bit dHash from row-major 9x8 grayscale pixels: each bit says whether a pixel
    /// is brighter than its right neighbour, so recompression and resizing barely change it.
    /// </summary>
    public static long DHash(ReadOnlySpan<byte> grayscale9x8)
    {
        if (grayscale9x8.Length != Width * Height)
        {
            throw new ArgumentException($"Expected {Width * Height} pixels.", nameof(grayscale9x8));
        }

        ulong hash = 0;
        var bit = 0;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width - 1; x++)
            {
                if (grayscale9x8[(y * Width) + x] > grayscale9x8[(y * Width) + x + 1])
                {
                    hash |= 1UL << bit;
                }

                bit++;
            }
        }

        return unchecked((long)hash);
    }

    public static int Distance(long a, long b) => BitOperations.PopCount(unchecked((ulong)(a ^ b)));
}
