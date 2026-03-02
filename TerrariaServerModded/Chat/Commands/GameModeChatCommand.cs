using Terraria;
using Terraria.ID;
using Terraria.Localization;
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
        var key = Main.GameMode switch
        {
            GameModeID.Normal => "Normal",
            GameModeID.Expert => "Expert",
            GameModeID.Master => "Master",
            GameModeID.Creative => "Creative",
            _ => "InvalidGameMode"
        };
        var mode = LanguageManager.Instance.GetTextValue($"UI.{key}");
        player.SendInfoMessage(mode);
    }
}