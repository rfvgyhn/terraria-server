using Terraria;

namespace TerrariaServerModded.Chat;

public interface IChatCommand
{
    static abstract string Description { get; }
    static abstract string DescriptionKey { get; }
    static abstract bool Matches(ReadOnlySpan<char> input);
    static abstract void Execute(Player player, AdditionalPlayerData? data, ReadOnlySpan<char> input);
}