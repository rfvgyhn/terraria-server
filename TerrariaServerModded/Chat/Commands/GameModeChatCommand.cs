using Terraria;
using Terraria.ID;
using Terraria.Localization;
using TerrariaServerModded.Cli.Commands;
using TerrariaServerModded.Extensions;

namespace TerrariaServerModded.Chat.Commands;

public class GameModeChatCommand : IChatCommand
{
    public static string Description => "/mode: Display the world's current game mode";
    public static string DescriptionKey => "ChatCommandDescription.GameMode";

    public static bool Matches(ReadOnlySpan<char> input) =>
        input.Equals("/mode", StringComparison.OrdinalIgnoreCase);

    public static void Execute(Player player, AdditionalPlayerData? data, ReadOnlySpan<char> input)
    {
        var mode = GameModeCliCommand.MapMode(Main.GameMode);
        player.SendInfoMessage(mode);
    }
}