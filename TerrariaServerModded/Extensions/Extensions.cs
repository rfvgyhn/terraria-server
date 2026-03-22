using System.Buffers;
using System.Numerics;

namespace TerrariaServerModded.Extensions;

public static class Extensions
{
    private static readonly SearchValues<char> InvalidFileNameChars =
        SearchValues.Create(Path.GetInvalidFileNameChars());

    public static string ToSafeFileName(this string? input, char replacement = '_')
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return string.Create(input.Length, input, (span, source) =>
        {
            source.AsSpan().CopyTo(span);

            int index;
            var globalOffset = 0;
            ReadOnlySpan<char> remaining = span;

            while ((index = remaining.IndexOfAny(InvalidFileNameChars)) != -1)
            {
                var targetIndex = globalOffset + index;
                span[targetIndex] = replacement;

                globalOffset = targetIndex + 1;
                remaining = span[globalOffset..];
            }
        });
    }

    public static bool[] UnpackBools(this uint value, int bitCount)
    {
        if (bitCount > 32)
            throw new ArgumentException("Too many bits for uint");

        var bits = new bool[bitCount];
        for (var i = 0; i < bitCount; i++)
            bits[i] = (value & (1u << i)) != 0;

        return bits;
    }

    public static uint PackBools(this bool[] bits)
    {
        if (bits.Length > 32)
            throw new ArgumentException("Too many bits for uint");

        var value = 0u;
        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i])
                value |= 1u << i;
        }

        return value;
    }
    
    public static ReadOnlySpan<byte> TrimWhitespace(this ReadOnlySpan<byte> input) => input.Trim(" \t\r\n\v\f"u8);
    
    /// <summary>
    /// Calculates the sum of the first <paramref name="count"/> elements in the array.
    /// This is a zero-allocation alternative to <c>items.Take(count).Sum()</c>.
    /// </summary>
    public static T SumUpTo<T>(this T[] items, int count) where T : INumber<T>
    {
        if (count <= 0) 
            return T.Zero;
        
        var sum = T.Zero;
        foreach (var item in items.AsSpan(0, Math.Min(count, items.Length)))
            sum += item;
        
        return sum;
    }
}