using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace TerrariaServerModded;

public class ColorJsonConverter : JsonConverter<Color>
{
    private const byte Comma = (byte)',';
    private const byte Space = (byte)' ';

    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string.");

        var span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

        var firstComma = span.IndexOf(Comma);
        if (firstComma == -1)
            throw new JsonException("Missing first comma");

        if (!Utf8Parser.TryParse(span[..firstComma].Trim(Space), out int red, out _))
            throw new JsonException("Invalid format for red component");

        var remaining = span[(firstComma + 1)..];
        var secondComma = remaining.IndexOf(Comma);
        if (secondComma == -1)
            throw new JsonException("Missing second comma");

        if (!Utf8Parser.TryParse(remaining[..secondComma].Trim(Space), out int green, out _))
            throw new JsonException("Invalid format for green component");

        if (!Utf8Parser.TryParse(remaining[(secondComma + 1)..].Trim(Space), out int blue, out _))
            throw new JsonException("Invalid format for blue component");

        return new Color(red, green, blue);
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        Span<byte> buffer = stackalloc byte[11];

        Utf8Formatter.TryFormat(value.R, buffer, out var bytesWritten);
        buffer[bytesWritten++] = Comma;

        Utf8Formatter.TryFormat(value.G, buffer[bytesWritten..], out var gLen);
        bytesWritten += gLen;
        buffer[bytesWritten++] = Comma;

        Utf8Formatter.TryFormat(value.B, buffer[bytesWritten..], out var bLen);
        bytesWritten += bLen;

        writer.WriteStringValue(buffer[..bytesWritten]);
    }
}