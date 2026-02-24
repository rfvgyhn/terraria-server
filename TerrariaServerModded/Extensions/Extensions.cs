using System.Buffers;

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
}