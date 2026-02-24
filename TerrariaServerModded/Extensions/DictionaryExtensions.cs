using System.Diagnostics.CodeAnalysis;
using Terraria;

namespace TerrariaServerModded.Extensions;

public static class DictionaryExtensions
{
    public static bool TryGet(
        this Dictionary<string, AdditionalPlayerData> dict,
        ReadOnlySpan<char> id,
        [NotNullWhen(true)] out AdditionalPlayerData? data)
    {
        data = null;
        return dict.TryGetAlternateLookup<ReadOnlySpan<char>>(out var lookup) && lookup.TryGetValue(id, out data);
    }

    public static bool TryGet(
        this Dictionary<string, AdditionalPlayerData> dict,
        Player player,
        [NotNullWhen(true)] out AdditionalPlayerData? data)
    {
        data = null;
        Span<char> idBuffer = stackalloc char[PlayerExtensions.MaxPlayerIdLength];
        if (!player.TryGetPlayerId(idBuffer, out var charsWritten))
            return false;

        ReadOnlySpan<char> id = idBuffer[..charsWritten];
        return dict.TryGet(id, out data);
    }

    public static bool TryRemove(
        this Dictionary<string, AdditionalPlayerData> dict,
        Player player,
        [NotNullWhen(true)] out AdditionalPlayerData? data)
    {
        data = null;
        Span<char> idBuffer = stackalloc char[PlayerExtensions.MaxPlayerIdLength];
        if (!player.TryGetPlayerId(idBuffer, out var charsWritten))
            return false;

        ReadOnlySpan<char> id = idBuffer[..charsWritten];
        return dict.TryGetAlternateLookup<ReadOnlySpan<char>>(out var lookup) && lookup.Remove(id, out _, out data);
    }
}