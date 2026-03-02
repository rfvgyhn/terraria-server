using Terraria;

namespace TerrariaServerModded.Chat;

[ChatCommandProcessor]
public static partial class ChatCommandProcessor
{
    public static partial void Init();
    public static partial bool ExecuteIfMatches(Player player, AdditionalPlayerData? data, ReadOnlySpan<char> input);
    public static partial void ListToClient(int playerIndex);
}